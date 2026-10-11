using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Passes copies of history messages down the pipeline instead of the messages themselves. Context compaction
/// (<c>CompactionProvider</c>) marks the messages it sends to the model as "from history" on the objects themselves,
/// and the agent then does not persist marked messages. After a turn with an edit approval the user's request vanished
/// from history and the model lost the task. The copies take the marks instead. The copy is shallow: contents are
/// shared, only the properties dictionary is new.
/// <para>
/// Tool result messages go down without an author and id. Within a run the tool loop's result messages have neither,
/// but the agent stamps both when it saves them, and compaction finds its place in the history by comparing the last
/// message it saw, author and id included: on every new run (the next message, an approved edit) it missed, rebuilt its
/// index from the raw history and compacted again, so each run after the first compaction broke the provider cache.
/// Models don't read these fields on tool results.
/// </para>
/// </summary>
internal sealed class DetachedMessagesChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(Detach(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(Detach(messages), options, cancellationToken);

    public static List<ChatMessage> Detach(IEnumerable<ChatMessage> messages) =>
    [
        .. messages.Select(message =>
        {
            var toolResults = message.Role == ChatRole.Tool;
            return new ChatMessage(message.Role, [.. message.Contents])
            {
                AuthorName = toolResults ? null : message.AuthorName,
                CreatedAt = message.CreatedAt,
                MessageId = toolResults ? null : message.MessageId,
                RawRepresentation = message.RawRepresentation,
                AdditionalProperties = message.AdditionalProperties is { } properties ? new AdditionalPropertiesDictionary(properties) : null,
            };
        }),
    ];
}
