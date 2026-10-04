namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>The service failed mid-stream (<see cref="StreamFailureChatClient"/>); the message is the service's text.</summary>
public sealed class ModelStreamException(string message) : Exception(message);
