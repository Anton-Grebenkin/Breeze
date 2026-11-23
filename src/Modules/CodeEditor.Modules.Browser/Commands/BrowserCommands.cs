using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Shell.Layout;

namespace CodeEditor.Modules.Browser.Commands;

/// <summary>
/// Browser palette commands, twins of what the panel shows: tabs (new, close, next, previous, pin), recent pages
/// (searchable by title and address), clearing the history and the tabs chip above the chat input. While the panel has
/// keyboard focus, <c>Ctrl+T</c>, <c>Ctrl+W</c> and <c>Ctrl+Tab</c> act on browser tabs, not editor ones.
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

        var focus = BrowserViewModel.FocusContextKey;
        var hasTabs = $"{focus} && {BrowserViewModel.HasTabsContextKey}";
        yield return Bind(keybindings, "Ctrl+T", NewTab, focus);
        yield return Bind(keybindings, "Ctrl+W", CloseTab, hasTabs);
        yield return Bind(keybindings, "Ctrl+F4", CloseTab, hasTabs);
        yield return Bind(keybindings, "Ctrl+Tab", NextTab, hasTabs);
        yield return Bind(keybindings, "Ctrl+Shift+Tab", PreviousTab, hasTabs);
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
