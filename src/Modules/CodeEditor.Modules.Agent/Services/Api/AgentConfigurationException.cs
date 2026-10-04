namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>The agent is not configured (no key, bad endpoint); the message tells the user what to fix.</summary>
public sealed class AgentConfigurationException(string message) : Exception(message);
