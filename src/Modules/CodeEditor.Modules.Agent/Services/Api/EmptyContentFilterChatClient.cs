using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Cleans history before sending: drops empty text blocks and gives argument-less calls an empty object (Anthropic
/// rejects <c>input: null</c>). Empty blocks come from the response stream (an empty chunk before tool calls); Anthropic
/// behind an OpenAI-compatible API rejects them, so the proxy replaces them with "[System: Empty message content
/// sanitised…]", which the model then repeats. Messages without empty blocks pass as is; one pass over the history.
/// </summary>
internal sealed class EmptyContentFilterChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(Clean(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(Clean(messages), options, cancellationToken);

    public static List<ChatMessage> Clean(IEnumerable<ChatMessage> messages) =>
        [.. messages.Select(Clean).Where(message => message.Contents.Count > 0)];

    private static ChatMessage Clean(ChatMessage message)
    {
        if (!message.Contents.Any(content => IsEmptyText(content) || IsCallWithoutArguments(content)))
        {
            return message;
        }

        return new ChatMessage(message.Role, [.. message.Contents.Where(content => !IsEmptyText(content)).Select(WithArguments)])
        {
            AuthorName = message.AuthorName,
            CreatedAt = message.CreatedAt,
            MessageId = message.MessageId,
            AdditionalProperties = message.AdditionalProperties,
        };
    }

    // A proxy placeholder in old history counts as empty too, otherwise the model repeats it.
    private static bool IsEmptyText(AIContent content) =>
        content is TextContent { Text: var text } && (string.IsNullOrWhiteSpace(text) || ProxyPlaceholderChatClient.IsPlaceholder(text));

    private static bool IsCallWithoutArguments(AIContent content) => content is FunctionCallContent { Arguments: null };

    // An argument-less call ("manage_todo()") gets an empty object: Anthropic answers "input: null" with 400.
    private static AIContent WithArguments(AIContent content) => content is FunctionCallContent { Arguments: null } call
        ? new FunctionCallContent(call.CallId, call.Name, new Dictionary<string, object?>()) { AdditionalProperties = call.AdditionalProperties }
        : content;
}
