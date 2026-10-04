using CodeEditor.Modules.Agent.Contracts.Approvals;

namespace CodeEditor.Modules.Git.Services.Agent;

/// <summary>
/// Approval of repository changes (ADR 0024): every <c>git_change</c> action goes through a card, and "Always allow"
/// stores the action in <c>git.alwaysAllow</c> (<see cref="ActionApprovalPolicy"/>). Push and restore always ask: push
/// leaves the machine (and runs only on the user's request), restore cannot be undone.
/// </summary>
public static class GitApprovals
{
    public const string AlwaysAllowKey = GitOptions.Section + ".alwaysAllow";

    public static ActionApprovalRules Rules { get; } =
        new(GitAgentTools.ChangeName, AlwaysAllowKey, GitCommands.ChangeActions, [GitCommands.Push, GitCommands.Restore]);
}
