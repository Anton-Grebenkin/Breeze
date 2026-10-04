using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>A user message queued during a turn (<see cref="UserMessageQueue"/>): feed text and the built model message.</summary>
/// <param name="Files">Names of attached files, shown under the question in the feed.</param>
/// <param name="Attachment">The open file the agent is told about.</param>
public sealed record QueuedMessage(string Text, ChatMessage Message, IReadOnlyList<string> Files, string? Attachment);
