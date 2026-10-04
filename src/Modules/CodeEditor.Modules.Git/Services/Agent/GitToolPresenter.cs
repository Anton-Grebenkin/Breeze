using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Git.Resources;

namespace CodeEditor.Modules.Git.Services.Agent;

/// <summary>
/// git rows in the agent feed ("Git status", "Git changes in Order.cs", "Commit: Fix discount"). Reads count as
/// exploration, so the feed collapses consecutive ones; the feed itself marks failures.
/// </summary>
public sealed class GitToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name switch
        {
            GitAgentTools.ReadName => new AgentToolView(AgentToolIcon.Git, ReadTitle(call)) { IsExploration = true, FilePath = call.Text("path") },
            GitAgentTools.ChangeName => new AgentToolView(AgentToolIcon.Git, ChangeTitle(call)),
            _ => null,
        };
    }

    private static string ReadTitle(AgentToolCall call) => (call.Text("action"), call.Text("path")) switch
    {
        (GitCommands.Diff, { } path) => Format(Strings.DiffOfTitle, path),
        (GitCommands.Diff, null) => Strings.DiffTitle,
        (GitCommands.Log, { } path) => Format(Strings.LogOfTitle, path),
        (GitCommands.Log, null) => Strings.LogTitle,
        (GitCommands.Show, _) => Format(Strings.ShowTitle, call.Text("revision") ?? "HEAD"),
        (GitCommands.Blame, var path) => Format(Strings.BlameTitle, path ?? "?"),
        _ => Strings.StatusTitle,
    };

    private static string ChangeTitle(AgentToolCall call) => call.Text("action") switch
    {
        GitCommands.Commit => Format(Strings.CommitTitle, FirstLine(call.Text("message"))),
        GitCommands.CreateBranch => Format(Strings.CreateBranchTitle, call.Text("branch") ?? "?"),
        GitCommands.Switch => Format(Strings.SwitchTitle, call.Text("branch") ?? "?"),
        GitCommands.Stash => Strings.StashTitle,
        GitCommands.StashPop => Strings.StashPopTitle,
        GitCommands.Restore => Format(Strings.RestoreTitle, call.Text("path") ?? "?"),
        GitCommands.Push => Strings.PushTitle,
        var action => "git " + action,
    };

    private static string FirstLine(string? text) => text is null ? "?" : text.Split('\n', 2)[0].Trim();

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
