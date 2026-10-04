namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>A tool error meant for the model: its text is returned as the call result so the model can fix the call.</summary>
public sealed class AgentToolException(string message) : Exception(message);
