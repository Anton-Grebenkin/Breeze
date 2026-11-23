using CodeEditor.Modules.Agent.Rules;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// What <see cref="GuardedToolFunction"/> adds to the main agent's calls: Deep mode advice, yielding to a message the
/// user wrote mid-turn, folder rules on a file's first read, the editor's notes, the calls' feed rows, the user's
/// hooks, and the modules' say on which calls only look and so run in every mode. Subagents run without them.
/// </summary>
public sealed record ToolGuards(
    DeepSupervisor Deep,
    UserMessageQueue Queue,
    ProjectRuleGuard? Rules = null,
    ToolCallObservers? Observers = null,
    RunningToolCalls? RunningCalls = null,
    HookRunner? Hooks = null,
    ApprovalCards? Approvals = null);
