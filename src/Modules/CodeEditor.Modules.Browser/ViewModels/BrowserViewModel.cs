using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.ToolWindows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>
/// The Browser panel (ADR 0027): address bar, back, forward, reload, history. The view (WebView2) shows the page and the
/// agent drives the same view, so the user sees every action and can take over. An address without a scheme gets http
/// for <c>localhost:5000</c> and https otherwise. "Show: Browser" puts the caret into the address bar. The history menu
/// lists recent pages; all of them are searchable in "Browser: Recent Pages".
/// </summary>
public sealed partial class BrowserViewModel : ObservableObject, IFocusableContent, IDisposable
{
    /// <summary>Pages in the address bar's history menu; the rest are in the palette.</summary>
    public const int MenuSize = 15;

    private const int MaxLabel = 90;

    private readonly IBrowserEngine _browser;
    private readonly BrowserHistory? _history;
    private readonly IQuickPick? _quickPick;

    public BrowserViewModel(IBrowserEngine browser, BrowserHistory? history = null, IQuickPick? quickPick = null)
    {
        _browser = browser;
        _history = history;
        _quickPick = quickPick;
        _browser.StateChanged += OnStateChanged;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoCommand))]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>The history menu is open; the view opens it under the button.</summary>
    [ObservableProperty]
    public partial bool IsHistoryOpen { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MenuItemViewModel> HistoryMenu { get; private set; } = [];

    public event EventHandler? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _browser.StateChanged -= OnStateChanged;

    /// <summary>Opens the address as if typed into the bar; load errors show on the panel.</summary>
    public Task OpenAsync(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        Address = url.AbsoluteUri;
        return GoAsync();
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

    /// <summary>"Title — localhost:5000/orders": history rows are found by either.</summary>
    public static string Label(BrowserVisit visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        var address = Uri.TryCreate(visit.Url, UriKind.Absolute, out var url) ? url.Authority + url.PathAndQuery.TrimEnd('/') : visit.Url;
        var label = visit.Title.Length > 0 ? visit.Title + " — " + address : address;
        return label.Length <= MaxLabel ? label : string.Concat(label.AsSpan(0, MaxLabel), "…");
    }

    [RelayCommand(CanExecute = nameof(CanGo))]
    private Task GoAsync() => RunAsync(token => Normalize(Address) is { } url ? _browser.NavigateAsync(url, token) : Task.CompletedTask);

    private bool CanGo() => Normalize(Address) is not null;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private Task BackAsync() => RunAsync(_browser.GoBackAsync);

    private bool CanGoBack() => _browser.CanGoBack;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private Task ForwardAsync() => RunAsync(_browser.GoForwardAsync);

    private bool CanGoForward() => _browser.CanGoForward;

    [RelayCommand]
    private Task ReloadAsync() => RunAsync(_browser.ReloadAsync);

    [RelayCommand]
    private void ShowHistory()
    {
        HistoryMenu = BuildHistoryMenu();
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

        var items = _history.Visits.Select(visit => new QuickPickItem(visit.Url, Label(visit))).ToList();
        _quickPick.Show(new QuickPickProvider(Strings.RecentPagesPlaceholder, items, item => OpenAsync(new Uri(item.Id))) { EmptyText = Strings.HistoryEmpty });
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ClearHistoryAsync()
    {
        _history?.Clear();
        return Task.CompletedTask;
    }

    private List<MenuItemViewModel> BuildHistoryMenu()
    {
        var visits = _history?.Visits ?? [];
        if (visits.Count == 0)
        {
            return [MenuItemViewModel.ForAction(Strings.HistoryEmpty, "Browser.History.Empty", new AsyncRelayCommand(() => Task.CompletedTask, () => false))];
        }

        return
        [
            .. visits.Take(MenuSize).Select((visit, index) =>
                MenuItemViewModel.ForAction(Label(visit), "Browser.History.Page" + index, new AsyncRelayCommand(() => OpenAsync(new Uri(visit.Url))))),
            MenuItemViewModel.CreateSeparator(),
            MenuItemViewModel.ForAction(Strings.AllPages, "Browser.History.All", ShowRecentPagesCommand),
            MenuItemViewModel.ForAction(Strings.ClearHistory, "Browser.History.Clear", ClearHistoryCommand),
        ];
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

    // Address and title come from the browser: both the agent and link navigation change them.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        Address = _browser.Url?.AbsoluteUri ?? Address;
        Title = _browser.Title;
        IsLoading = _browser.IsLoading;
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }
}
