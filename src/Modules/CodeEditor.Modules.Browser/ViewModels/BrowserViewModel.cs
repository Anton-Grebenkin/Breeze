using System.Collections.ObjectModel;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>
/// The Browser panel (ADR 0027, 0054): a tab strip, and for the shown tab the address bar, back, forward, reload and
/// history. The view (WebView2) shows the pages; the agent works in the same tabs, so the user sees every action and can
/// take over. An address without a scheme gets http for <c>localhost:5000</c> and https otherwise. "Show: Browser" puts
/// the caret into the address bar.
/// </summary>
public sealed partial class BrowserViewModel : ObservableObject, IFocusableContent, IDisposable
{
    /// <summary>Context key: keyboard focus is in the panel, so <c>Ctrl+T</c>, <c>Ctrl+W</c> and <c>Ctrl+Tab</c> act on its tabs.</summary>
    public const string FocusContextKey = "browserFocus";

    /// <summary>Context key: the browser has a tab to close or switch to.</summary>
    public const string HasTabsContextKey = "browserHasTabs";

    private readonly BrowserTabs _tabs;
    private readonly BrowserTabStrip _strip;
    private readonly IUiDispatcher _dispatcher;
    private readonly BrowserHistory? _history;
    private readonly IQuickPick? _quickPick;
    private readonly IContextKeyService? _context;
    private (BrowserTab? Tab, Uri? Url) _shown;

    public BrowserViewModel(
        BrowserTabs tabs, IUiDispatcher dispatcher, BrowserHistory? history = null, IQuickPick? quickPick = null, IContextKeyService? context = null,
        ICommandService? commands = null, ISystemShell? shell = null, StatusBarViewModel? statusBar = null)
    {
        Tools = new BrowserPageToolsViewModel(tabs, commands, shell, statusBar);
        _tabs = tabs;
        _strip = new BrowserTabStrip(tabs);
        _dispatcher = dispatcher;
        _history = history;
        _quickPick = quickPick;
        _context = context;
        _tabs.Changed += OnTabsChanged;
        _tabs.PageChanged += OnPageChanged;
        Sync();
    }

    public ObservableCollection<BrowserTabViewModel> Tabs => _strip.Items;

    /// <summary>Find on page, zoom, downloads, developer tools and sending to the chat, for the shown page.</summary>
    public BrowserPageToolsViewModel Tools { get; }

    /// <summary>The shown tab; the view shows its page.</summary>
    [ObservableProperty]
    public partial BrowserTabViewModel? ActiveTab { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoCommand))]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>The shown tab has a page or is loading one; an empty tab shows a hint instead of a blank page.</summary>
    [ObservableProperty]
    public partial bool IsPageShown { get; private set; }

    /// <summary>The history menu is open; the view opens it under the button.</summary>
    [ObservableProperty]
    public partial bool IsHistoryOpen { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MenuItemViewModel> HistoryMenu { get; private set; } = [];

    public event EventHandler? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public void SetFocused(bool focused) => _context?.Set(FocusContextKey, focused);

    public void Dispose()
    {
        _tabs.Changed -= OnTabsChanged;
        _tabs.PageChanged -= OnPageChanged;
        _context?.Set(FocusContextKey, false);
    }

    /// <summary>
    /// Opens the address as if typed into the bar, in a new tab unless the shown one is empty; load errors show on the
    /// panel.
    /// </summary>
    public Task OpenAsync(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (_tabs.Active is not { HasWebPage: false })
        {
            OpenTab();
        }

        return NavigateAsync(url);
    }

    /// <summary>Address from the bar: https without a scheme, http for this machine.</summary>
    public static Uri? Normalize(string address)
    {
        var text = address.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            var local = text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || text.StartsWith("127.0.0.1", StringComparison.Ordinal);
            text = (local ? "http://" : "https://") + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" ? url : null;
    }

    [RelayCommand(CanExecute = nameof(CanGo))]
    private Task GoAsync() => Normalize(Address) is { } url ? NavigateAsync(url) : Task.CompletedTask;

    private bool CanGo() => Normalize(Address) is not null;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private Task BackAsync() => RunAsync(token => _tabs.Active?.Page.GoBackAsync(token) ?? Task.CompletedTask);

    private bool CanGoBack() => _tabs.Active?.Page.CanGoBack == true;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private Task ForwardAsync() => RunAsync(token => _tabs.Active?.Page.GoForwardAsync(token) ?? Task.CompletedTask);

    private bool CanGoForward() => _tabs.Active?.Page.CanGoForward == true;

    [RelayCommand]
    private Task ReloadAsync() => RunAsync(token => _tabs.Active?.Page.ReloadAsync(token) ?? Task.CompletedTask);

    /// <summary>A new empty tab with the caret in the address bar.</summary>
    [RelayCommand]
    private void NewTab()
    {
        OpenTab();
        RequestFocus();
    }

    [RelayCommand]
    private void CloseTab()
    {
        if (_tabs.Active is { } tab)
        {
            _tabs.Close(tab);
        }
    }

    [RelayCommand]
    private void NextTab() => _tabs.SelectNext(1);

    [RelayCommand]
    private void PreviousTab() => _tabs.SelectNext(-1);

    [RelayCommand]
    private void TogglePin()
    {
        if (_tabs.Active is { } tab)
        {
            _tabs.SetPinned(tab, !tab.IsPinned);
        }
    }

    [RelayCommand]
    private void ShowHistory()
    {
        HistoryMenu = BrowserHistoryMenu.Build(_history?.Visits ?? [], OpenAsync, ShowRecentPagesCommand, ClearHistoryCommand);
        IsHistoryOpen = true;
    }

    /// <summary>The whole history in the quick pick, searchable by title and address.</summary>
    [RelayCommand]
    private Task ShowRecentPagesAsync()
    {
        if (_quickPick is null || _history is null)
        {
            return Task.CompletedTask;
        }

        var items = _history.Visits.Select(visit => new QuickPickItem(visit.Url, BrowserHistoryMenu.Label(visit))).ToList();
        _quickPick.Show(new QuickPickProvider(Strings.RecentPagesPlaceholder, items, item => OpenAsync(new Uri(item.Id))) { EmptyText = Strings.HistoryEmpty });
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ClearHistoryAsync()
    {
        _history?.Clear();
        return Task.CompletedTask;
    }

    private Task NavigateAsync(Uri url)
    {
        var page = (_tabs.Active ?? OpenTab()).Page;
        Address = url.AbsoluteUri;
        return RunAsync(token => page.NavigateAsync(url, token));
    }

    // The strip follows at once: the queued update would otherwise clear the address typed for the new tab.
    private BrowserTab OpenTab()
    {
        var tab = _tabs.Open();
        Sync();
        return tab;
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        Error = null;
        try
        {
            await action(CancellationToken.None);
        }
        catch (BrowserException exception)
        {
            Error = exception.Message;
        }
    }

    // The agent changes tabs from its own thread.
    private void OnTabsChanged(object? sender, EventArgs e) => _dispatcher.Post(Sync);

    private void OnPageChanged(object? sender, BrowserTabEventArgs e) => _dispatcher.Post(() =>
    {
        _strip.Refresh(e.Tab);
        if (e.Tab == _tabs.Active)
        {
            ShowActivePage();
        }
    });

    private void Sync()
    {
        ActiveTab = _strip.Sync();
        _context?.Set(HasTabsContextKey, Tabs.Count > 0);
        ShowActivePage();
    }

    // Address and title come from the shown page: both the agent and link navigation change them. The bar is rewritten
    // only when the page or its address changed, so a title update does not erase what the user is typing.
    private void ShowActivePage()
    {
        var tab = _tabs.Active;
        var page = tab?.Page;
        if (_shown != (tab, page?.Url))
        {
            if (_shown.Tab != tab)
            {
                Error = null;
            }

            _shown = (tab, page?.Url);
            Address = page?.Url?.AbsoluteUri ?? string.Empty;
        }

        Title = page?.Title ?? string.Empty;
        IsLoading = page?.IsLoading ?? false;
        IsPageShown = page is not null && (page.IsLoading || page.Url is { Scheme: not "about" });
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }
}
