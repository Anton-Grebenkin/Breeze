using CodeEditor.Core.Commands;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.ViewModels;

namespace CodeEditor.Modules.Browser.Commands;

/// <summary>
/// Browser palette commands, twins of the history menu in the address bar: recent pages (searchable by title and
/// address) and clearing the history; and the keyboard twin of the tabs chip above the chat input.
/// </summary>
public sealed class BrowserCommands(Func<BrowserViewModel> panel, BrowserTabsSwitch tabs)
{
    public const string RecentPages = "browser.recentPages";
    public const string ClearHistory = "browser.clearHistory";
    public const string ShareTabs = "browser.shareTabsWithAgent";

    private static string Category => Strings.ModuleName;

    public IEnumerable<IDisposable> Register(ICommandRegistry commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        yield return commands.Register(new CommandDefinition(RecentPages, Strings.OpenRecentCommand, (_, _) => new ValueTask(panel().ShowRecentPagesCommand.ExecuteAsync(null)), Category));
        yield return commands.Register(new CommandDefinition(ShareTabs, Strings.ShareTabsCommand, (_, _) =>
        {
            tabs.Toggle();
            return ValueTask.CompletedTask;
        }, Category));
        yield return commands.Register(new CommandDefinition(ClearHistory, Strings.ClearHistoryCommand, (_, _) => new ValueTask(panel().ClearHistoryCommand.ExecuteAsync(null)), Category));
    }
}
