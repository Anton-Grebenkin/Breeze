namespace CodeEditor.Modules.Agent.Services.Hooks;

/// <summary>
/// User hooks by event (<c>agent.hooks</c>), as in Claude Code: commands that check or steer the agent. Read only from
/// the user's settings, never from a folder's: a hook runs any command.
/// </summary>
public sealed class AgentHookSettings
{
    /// <summary>Before a tool call; exit code 2 blocks it, and the output tells the model why.</summary>
    public List<AgentHook> PreToolUse { get; set; } = [];

    /// <summary>After a tool call; exit code 2 hands the output to the model with the result (a failed lint, tests).</summary>
    public List<AgentHook> PostToolUse { get; set; } = [];

    /// <summary>Before a message goes to the model; exit code 2 stops it, otherwise the output joins it as context.</summary>
    public List<AgentHook> UserPromptSubmit { get; set; } = [];

    /// <summary>When the agent is about to finish; exit code 2 makes it go on, with the output as the reason.</summary>
    public List<AgentHook> Stop { get; set; } = [];
}
