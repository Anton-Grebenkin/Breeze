namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>A tool error meant for the model: its text is returned as the call result so the model can fix the call.</summary>
public sealed class AgentToolException(string message) : Exception(message)
{
    /// <summary>
    /// The tool cannot work in this environment at all (antivirus blocks every command, say): the harness switches it
    /// off until the end of the turn instead of letting the model repeat the call.
    /// </summary>
    public bool ToolUnavailable { get; init; }
}
