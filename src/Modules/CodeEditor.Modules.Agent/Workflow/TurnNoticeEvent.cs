using Microsoft.Agents.AI.Workflows;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>A harness line for the feed: a check result (status) or an advisor note (work log).</summary>
public sealed class TurnNoticeEvent(ChatMessageKind kind, string text) : WorkflowEvent
{
    public ChatMessageKind Kind { get; } = kind;

    public string Text { get; } = text;
}
