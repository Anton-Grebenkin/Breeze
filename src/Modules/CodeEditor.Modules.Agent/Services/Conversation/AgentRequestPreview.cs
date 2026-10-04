using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// The fixed part of every model request (system prompt and tools) as the model sees it with current settings. Shows
/// what is sent to the model without network access: for the benchmark (<c>--show-prompt</c>) and debugging.
/// </summary>
public sealed record AgentRequestPreview(string Instructions, IReadOnlyList<AITool> Tools);
