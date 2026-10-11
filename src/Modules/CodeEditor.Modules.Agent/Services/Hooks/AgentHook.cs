namespace CodeEditor.Modules.Agent.Services.Hooks;

/// <summary>A user hook (<see cref="AgentHookSettings"/>): a PowerShell command run on an agent event.</summary>
public sealed class AgentHook
{
    /// <summary>Tools it applies to, as a regex on the tool name (<c>run_command|apply_patch</c>); empty — every tool.</summary>
    public string? Matcher { get; set; }

    /// <summary>A PowerShell command; the event comes as JSON on standard input.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Time limit in seconds; <c>null</c> for <see cref="HookRunner.DefaultTimeoutSeconds"/>.</summary>
    public int? TimeoutSeconds { get; set; }
}
