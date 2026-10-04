using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>What arrived in one response: the answer, reasoning, tool calls and approval requests.</summary>
internal sealed class ResponseState(ChatMessageViewModel answer)
{
    public ChatMessageViewModel Answer { get; } = answer;

    public ChatMessageViewModel? Reasoning { get; set; }

    /// <summary>Splits reasoning written in tags out of the answer text.</summary>
    public ThinkingTagSplitter Tags { get; } = new();

    public Dictionary<string, (ChatMessageViewModel Message, FunctionCallContent Call)> Tools { get; } = new(StringComparer.Ordinal);

    public List<ToolApprovalRequestContent> Requests { get; } = [];

    public ChatFinishReason? FinishReason { get; set; }

    /// <summary>Token usage per request; a repeated report within a request does not double the count.</summary>
    public UsageAccumulator Usage { get; } = new();
}
