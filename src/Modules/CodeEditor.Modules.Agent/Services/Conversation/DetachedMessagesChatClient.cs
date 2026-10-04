using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Passes copies of history messages down the pipeline instead of the messages themselves. Context compaction
/// (<c>CompactionProvider</c>) marks the messages it sends to the model as "from history" on the objects themselves,
/// and the agent then does not persist marked messages. After a turn with an edit approval the user's request vanished
/// from history and the model lost the task. The copies take the marks instead. The copy is shallow: contents are
/// shared, only the properties dictionary is new.
/// </summary>
internal sealed class DetachedMessagesChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(Detach(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(Detach(messages), options, cancellationToken);

    public static List<ChatMessage> Detach(IEnumerable<ChatMessage> messages) =>
    [
        .. messages.Select(message => new ChatMessage(message.Role, [.. message.Contents])
        {
            AuthorName = message.AuthorName,
            CreatedAt = message.CreatedAt,
            MessageId = message.MessageId,
            RawRepresentation = message.RawRepresentation,
            AdditionalProperties = message.AdditionalProperties is { } properties ? new AdditionalPropertiesDictionary(properties) : null,
        }),
    ];
}
