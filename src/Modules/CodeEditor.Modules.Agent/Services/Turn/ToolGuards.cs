using CodeEditor.Modules.Agent.Rules;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// What <see cref="GuardedToolFunction"/> adds to the main agent's calls: Deep mode advice, yielding to a message the
/// user wrote mid-turn, folder rules on a file's first read, the editor's notes, the calls' feed rows and the user's
/// hooks. Subagents run without them.
/// </summary>
public sealed record ToolGuards(
    DeepSupervisor Deep,
    UserMessageQueue Queue,
    ProjectRuleGuard? Rules = null,
    ToolCallObservers? Observers = null,
    RunningToolCalls? RunningCalls = null,
    HookRunner? Hooks = null);
