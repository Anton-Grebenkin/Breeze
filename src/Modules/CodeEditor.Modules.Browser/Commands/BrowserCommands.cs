using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Shell.Layout;

namespace CodeEditor.Modules.Browser.Commands;

/// <summary>
/// Browser palette commands, twins of what the panel shows: tabs (new, close, next, previous, pin), the tools of the
/// shown page (find, zoom, downloads, developer tools, the system browser, sending to the chat), recent pages
/// (searchable by title and address), clearing the history and the tabs chip above the chat input. While the panel has
/// keyboard focus the usual browser keys work: <c>Ctrl+T</c>, <c>Ctrl+W</c>, <c>Ctrl+Tab</c>, <c>Ctrl+L</c>,
/// <c>Ctrl+F</c>, <c>F5</c>, <c>F12</c>, <c>Ctrl+=</c>, <c>Ctrl+-</c>, <c>Ctrl+0</c>, <c>Ctrl+J</c>.
/// </summary>
public sealed class BrowserCommands(Func<BrowserViewModel> panel, BrowserTabsSwitch tabs, WorkbenchLayout layout)
{
    public const string RecentPages = "browser.recentPages";
    public const string ClearHistory = "browser.clearHistory";
    public const string ShareTabs = "browser.shareTabsWithAgent";
    public const string NewTab = "browser.newTab";
    public const string CloseTab = "browser.closeTab";
    public const string CloseOtherTabs = "browser.closeOtherTabs";
    public const string NextTab = "browser.nextTab";
    public const string PreviousTab = "browser.previousTab";
    public const string PinTab = "browser.pinTab";
    public const string FocusAddress = "browser.focusAddress";
    public const string Reload = "browser.reload";
    public const string Find = "browser.find";
    public const string ZoomIn = "browser.zoomIn";
    public const string ZoomOut = "browser.zoomOut";
    public const string ZoomReset = "browser.zoomReset";
    public const string Downloads = "browser.downloads";
    public const string DevTools = "browser.devTools";
    public const string OpenExternal = "browser.openExternal";
    public const string SendPage = "browser.sendPageToChat";
    public const string SendSelection = "browser.sendSelectionToChat";
    public const string PickElement = "browser.pickElementForChat";

    private static string Category => Strings.ModuleName;

    public IEnumerable<IDisposable> Register(ICommandRegistry commands, IKeybindingRegistry keybindings)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(keybindings);
        yield return commands.Register(new CommandDefinition(RecentPages, Strings.OpenRecentCommand, (_, _) => new ValueTask(panel().ShowRecentPagesCommand.ExecuteAsync(null)), Category));
        yield return commands.Register(new CommandDefinition(ClearHistory, Strings.ClearHistoryCommand, (_, _) => new ValueTask(panel().ClearHistoryCommand.ExecuteAsync(null)), Category));
        yield return Add(commands, ShareTabs, Strings.ShareTabsCommand, tabs.Toggle);
        yield return Add(commands, NewTab, Strings.NewTabCommand, () =>
        {
            layout.Show(BrowserModule.ToolWindowId);
            panel().NewTabCommand.Execute(null);
        });
        yield return Add(commands, CloseTab, Strings.CloseTabCommand, () => panel().CloseTabCommand.Execute(null));
        yield return Add(commands, CloseOtherTabs, Strings.CloseOtherTabsCommand, () => panel().ActiveTab?.CloseOthersCommand.Execute(null));
        yield return Add(commands, NextTab, Strings.NextTabCommand, () => panel().NextTabCommand.Execute(null));
        yield return Add(commands, PreviousTab, Strings.PreviousTabCommand, () => panel().PreviousTabCommand.Execute(null));
        yield return Add(commands, PinTab, Strings.PinTabCommand, () => panel().TogglePinCommand.Execute(null));

        yield return Add(commands, FocusAddress, Strings.FocusAddressCommand, () => panel().RequestFocus());
        yield return Add(commands, Reload, Strings.Reload, () => panel().ReloadCommand.Execute(null));
        yield return Add(commands, Find, Strings.FindOnPage, () => panel().Tools.ShowFindCommand.Execute(null));
        yield return Add(commands, ZoomIn, Strings.ZoomIn, () => panel().Tools.ZoomInCommand.Execute(null));
        yield return Add(commands, ZoomOut, Strings.ZoomOut, () => panel().Tools.ZoomOutCommand.Execute(null));
        yield return Add(commands, ZoomReset, Strings.ZoomResetCommand, () => panel().Tools.ZoomResetCommand.Execute(null));
        yield return Add(commands, Downloads, Strings.Downloads, () => panel().Tools.DownloadsCommand.Execute(null));
        yield return Add(commands, DevTools, Strings.DevTools, () => panel().Tools.DevToolsCommand.Execute(null));
        yield return Add(commands, OpenExternal, Strings.OpenExternal, () => panel().Tools.OpenExternalCommand.Execute(null));
        yield return Add(commands, SendPage, Strings.SendPageToChat, () => panel().Tools.SendPageCommand.Execute(null));
        yield return Add(commands, SendSelection, Strings.SendSelectionToChat, () => panel().Tools.SendSelectionCommand.Execute(null));
        yield return Add(commands, PickElement, Strings.PickElementForChat, () => panel().Tools.PickElementCommand.Execute(null));

        var focus = BrowserViewModel.FocusContextKey;
        var hasTabs = $"{focus} && {BrowserViewModel.HasTabsContextKey}";
        yield return Bind(keybindings, "Ctrl+T", NewTab, focus);
        yield return Bind(keybindings, "Ctrl+W", CloseTab, hasTabs);
        yield return Bind(keybindings, "Ctrl+F4", CloseTab, hasTabs);
        yield return Bind(keybindings, "Ctrl+Tab", NextTab, hasTabs);
        yield return Bind(keybindings, "Ctrl+Shift+Tab", PreviousTab, hasTabs);
        yield return Bind(keybindings, "Ctrl+L", FocusAddress, focus);
        yield return Bind(keybindings, "F5", Reload, hasTabs);
        yield return Bind(keybindings, "Ctrl+R", Reload, hasTabs);
        yield return Bind(keybindings, "Ctrl+F", Find, hasTabs);

        // Interface zoom has these keys everywhere else; in the browser they zoom the page, as in any browser.
        yield return Bind(keybindings, "Ctrl+=", ZoomIn, hasTabs);
        yield return Bind(keybindings, "Ctrl+-", ZoomOut, hasTabs);
        yield return Bind(keybindings, "Ctrl+0", ZoomReset, hasTabs);
        yield return Bind(keybindings, "Ctrl+J", Downloads, focus);
        yield return Bind(keybindings, "F12", DevTools, hasTabs);
        yield return Bind(keybindings, "Ctrl+Shift+C", PickElement, hasTabs);
    }

    private static IDisposable Add(ICommandRegistry commands, string id, string title, Action action) =>
        commands.Register(new CommandDefinition(id, title, (_, _) =>
        {
            action();
            return ValueTask.CompletedTask;
        }, Category));

    private static IDisposable Bind(IKeybindingRegistry keybindings, string keys, string commandId, string when) =>
        keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, ContextExpression.Parse(when)));
}
