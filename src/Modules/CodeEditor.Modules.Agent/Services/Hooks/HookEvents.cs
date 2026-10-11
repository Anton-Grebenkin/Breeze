namespace CodeEditor.Modules.Agent.Services.Hooks;

/// <summary>Hook event names, as in Claude Code and in the hook's JSON (<c>hook_event_name</c>).</summary>
public static class HookEvents
{
    public const string PreToolUse = "PreToolUse";
    public const string PostToolUse = "PostToolUse";
    public const string UserPromptSubmit = "UserPromptSubmit";
    public const string Stop = "Stop";
}
