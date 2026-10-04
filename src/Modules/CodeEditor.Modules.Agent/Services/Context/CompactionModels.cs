using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>Models that write the compaction summary (<see cref="ConversationSummaryStrategy"/>).</summary>
/// <param name="Conversation">The conversation model without the harness: the same client that gets chat requests.</param>
/// <param name="ConversationOptions">Options of the last chat request; <c>null</c> if there were none yet.</param>
/// <param name="Helper">The helper model, used when the conversation request cannot be repeated.</param>
public sealed record CompactionModels(IChatClient Conversation, Func<ChatOptions?> ConversationOptions, IChatClient Helper);
