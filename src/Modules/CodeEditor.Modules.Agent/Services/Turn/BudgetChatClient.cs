using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Counts turn requests (<see cref="TurnBudget"/>). The last allowed request goes out with tools disabled
/// (<c>tool_choice: none</c>) and a wrap-up request. Tool definitions stay in the request: Anthropic rejects a history
/// with tool calls but no tool definitions.
/// </summary>
internal sealed class BudgetChatClient(IChatClient inner, TurnBudget budget) : DelegatingChatClient(inner)
{
    public static string WrapUpRequest => Strings.WrapUpRequest;

    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        budget.TryStartRequest()
            ? base.GetResponseAsync(messages, options, cancellationToken)
            : base.GetResponseAsync(WithWrapUp(messages), WithoutTools(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        budget.TryStartRequest()
            ? base.GetStreamingResponseAsync(messages, options, cancellationToken)
            : base.GetStreamingResponseAsync(WithWrapUp(messages), WithoutTools(options), cancellationToken);

    private static List<ChatMessage> WithWrapUp(IEnumerable<ChatMessage> messages) =>
        [.. messages, new ChatMessage(ChatRole.User, PromptSections.EditorNote(WrapUpRequest))];

    private static ChatOptions WithoutTools(ChatOptions? options)
    {
        var limited = options?.Clone() ?? new ChatOptions();
        limited.ToolMode = ChatToolMode.None;
        return limited;
    }
}
