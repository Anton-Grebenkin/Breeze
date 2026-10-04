using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>Outcome of a model round: approval requests, finish reason and the final text (without the work log before calls).</summary>
public sealed record ModelOutcome(IReadOnlyList<ToolApprovalRequestContent> Requests, ChatFinishReason? FinishReason, string Answer)
{
    /// <summary>The model awaits decisions on edits or commands; the round continues through the approvals node.</summary>
    public bool NeedsApproval => Requests.Count > 0;
}
