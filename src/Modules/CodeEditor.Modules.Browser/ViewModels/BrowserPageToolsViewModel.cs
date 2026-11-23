using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Commands;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Browser.ViewModels;

/// <summary>
/// What a person does with the shown page besides reading it, behind the "more" button of the Browser panel and in the
/// palette: find on page, zoom, downloads, developer tools, the same page in the system browser, and sending the page,
/// the selected text or a picked element to the agent's chat.
/// </summary>
public sealed partial class BrowserPageToolsViewModel : ObservableObject
{
    /// <summary>Selected text longer than this is cut before it goes to the chat.</summary>
    public const int MaxSelection = 4000;

    private static readonly double[] ZoomLevels = [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5];

    private readonly BrowserTabs _tabs;
    private readonly ICommandService? _commands;
    private readonly ISystemShell? _shell;
    private readonly StatusBarViewModel? _statusBar;
    private CancellationTokenSource? _picking;

    public BrowserPageToolsViewModel(BrowserTabs tabs, ICommandService? commands = null, ISystemShell? shell = null, StatusBarViewModel? statusBar = null)
    {
        _tabs = tabs;
        _commands = commands;
        _shell = shell;
        _statusBar = statusBar;
    }

    [ObservableProperty]
    public partial bool IsFindOpen { get; private set; }

    [ObservableProperty]
    public partial string FindText { get; set; } = string.Empty;

    /// <summary>"2 of 5", "no matches" or empty.</summary>
    [ObservableProperty]
    public partial string FindStatus { get; private set; } = string.Empty;

    /// <summary>The user is choosing an element on the page; a click picks it, Escape cancels.</summary>
    [ObservableProperty]
    public partial bool IsPicking { get; private set; }

    /// <summary>The "more" menu is open; the view opens it under the button.</summary>
    [ObservableProperty]
    public partial bool IsMenuOpen { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MenuItemViewModel> Menu { get; private set; } = [];

    /// <summary>The find box wants the caret.</summary>
    public event EventHandler? FindFocusRequested;

    private BrowserTab? Tab => _tabs.Active is { HasWebPage: true } tab ? tab : null;

    [RelayCommand]
    private Task ShowMenuAsync()
    {
        Menu =
        [
            MenuItemViewModel.ForAction(Strings.FindOnPage, "Browser.More.Find", ShowFindCommand),
            MenuItemViewModel.CreateSeparator(),
            MenuItemViewModel.ForAction(Strings.ZoomIn, "Browser.More.ZoomIn", ZoomInCommand),
            MenuItemViewModel.ForAction(Strings.ZoomOut, "Browser.More.ZoomOut", ZoomOutCommand),
            MenuItemViewModel.ForAction(string.Format(CultureInfo.CurrentCulture, Strings.ZoomReset, Math.Round((Tab?.Page.Zoom ?? 1) * 100)), "Browser.More.ZoomReset", ZoomResetCommand),
            MenuItemViewModel.CreateSeparator(),
            MenuItemViewModel.ForAction(Strings.SendPageToChat, "Browser.More.SendPage", SendPageCommand),
            MenuItemViewModel.ForAction(Strings.SendSelectionToChat, "Browser.More.SendSelection", SendSelectionCommand),
            MenuItemViewModel.ForAction(Strings.PickElementForChat, "Browser.More.PickElement", PickElementCommand),
            MenuItemViewModel.CreateSeparator(),
            MenuItemViewModel.ForAction(Strings.Downloads, "Browser.More.Downloads", DownloadsCommand),
            MenuItemViewModel.ForAction(Strings.DevTools, "Browser.More.DevTools", DevToolsCommand),
            MenuItemViewModel.ForAction(Strings.OpenExternal, "Browser.More.OpenExternal", OpenExternalCommand),
        ];
        IsMenuOpen = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ShowFindAsync()
    {
        IsFindOpen = true;
        FindFocusRequested?.Invoke(this, EventArgs.Empty);
        return FindAsync(BrowserFindStep.Start);
    }

    [RelayCommand]
    private Task FindNextAsync() => FindAsync(BrowserFindStep.Next);

    [RelayCommand]
    private Task FindPreviousAsync() => FindAsync(BrowserFindStep.Previous);

    [RelayCommand]
    private async Task CloseFindAsync()
    {
        IsFindOpen = false;
        FindStatus = string.Empty;
        if (Tab is { } tab)
        {
            await QuietlyAsync(() => tab.Page.FindAsync(string.Empty, BrowserFindStep.Start, CancellationToken.None));
        }
    }

    [RelayCommand]
    private Task ZoomInAsync() => ZoomAsync(step: 1);

    [RelayCommand]
    private Task ZoomOutAsync() => ZoomAsync(step: -1);

    [RelayCommand]
    private Task ZoomResetAsync() => Tab is { } tab ? tab.Page.SetZoomAsync(1, CancellationToken.None) : Task.CompletedTask;

    [RelayCommand]
    private Task DevToolsAsync() => Tab is { } tab ? QuietlyAsync(() => tab.Page.OpenAsync(BrowserWindowKind.DevTools, CancellationToken.None)) : Task.CompletedTask;

    // The downloads list is the browser's own, not a page's: any tab opens it.
    [RelayCommand]
    private Task DownloadsAsync() =>
        _tabs.Active is { } tab ? QuietlyAsync(() => tab.Page.OpenAsync(BrowserWindowKind.Downloads, CancellationToken.None)) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenExternalAsync()
    {
        if (Tab is { } tab)
        {
            _shell?.OpenInBrowser(tab.Page.Url!);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SendPageAsync() =>
        Tab is { } tab ? ToChatAsync(Format(Strings.ChatPage, tab.Id, tab.Page.Url!.AbsoluteUri)) : Task.CompletedTask;

    [RelayCommand]
    private async Task SendSelectionAsync()
    {
        if (Tab is not { } tab)
        {
            return;
        }

        var selected = await QuietlyAsync(() => tab.Page.EvaluateAsync("window.getSelection().toString()", CancellationToken.None));
        var text = selected is { Length: > 0 } ? JsonSerializer.Deserialize<string>(selected)?.Trim() : null;
        if (string.IsNullOrEmpty(text))
        {
            Tell(Strings.NothingSelected);
            return;
        }

        var cut = text.Length > MaxSelection ? string.Concat(text.AsSpan(0, MaxSelection), "…") : text;
        await ToChatAsync(Format(Strings.ChatSelection, tab.Id, tab.Page.Url!.AbsoluteUri, "> " + cut.ReplaceLineEndings("\n> ")));
    }

    /// <summary>Starts picking an element; run again while picking, it cancels.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PickElementAsync()
    {
        if (_picking is { } going)
        {
            await going.CancelAsync();
            return;
        }

        if (Tab is not { } tab)
        {
            return;
        }

        using var picking = _picking = new CancellationTokenSource();
        IsPicking = true;
        Tell(Strings.PickElementHint);
        try
        {
            if (await QuietlyAsync(() => tab.Page.PickElementAsync(picking.Token)) is { } picked)
            {
                await ToChatAsync(Format(Strings.ChatElement, tab.Id, tab.Page.Url!.AbsoluteUri, picked.Tag, picked.Text, picked.Selector, picked.Html));
            }
        }
        finally
        {
            (_picking, IsPicking) = (null, false);
        }
    }

    partial void OnFindTextChanged(string value) => _ = FindAsync(BrowserFindStep.Start);

    private async Task FindAsync(BrowserFindStep step)
    {
        if (!IsFindOpen || Tab is not { } tab)
        {
            return;
        }

        var found = await QuietlyAsync(() => tab.Page.FindAsync(FindText, step, CancellationToken.None));
        FindStatus = FindText.Length == 0 ? string.Empty
            : found.Count == 0 ? Strings.FindNoMatches
            : Format(Strings.FindPosition, found.Active, found.Count);
    }

    // The nearest standard level above or below the current zoom, as browsers step.
    private Task ZoomAsync(int step)
    {
        if (Tab is not { } tab)
        {
            return Task.CompletedTask;
        }

        var nearest = Array.IndexOf(ZoomLevels, ZoomLevels.MinBy(level => Math.Abs(level - tab.Page.Zoom)));
        return tab.Page.SetZoomAsync(ZoomLevels[Math.Clamp(nearest + step, 0, ZoomLevels.Length - 1)], CancellationToken.None);
    }

    private async Task ToChatAsync(string text)
    {
        if (_commands is not null)
        {
            // Line ends as in the input box, whatever the resource file has.
            await _commands.ExecuteAsync(AgentChatCommands.AddToInput, text.ReplaceLineEndings("\n"));
        }
    }

    private void Tell(string message)
    {
        if (_statusBar is not null)
        {
            _statusBar.Message = message;
        }
    }

    // A page that is gone or busy answers with an error the user can do nothing about: the action just does not happen.
    private static async Task<T?> QuietlyAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (BrowserException)
        {
            return default;
        }
    }

    private static async Task QuietlyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (BrowserException)
        {
            // See above.
        }
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
