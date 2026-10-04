namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>Request of the <c>approve</c> port: the cards the user decides on (automatic ones are excluded).</summary>
public sealed record ApprovalPending(IReadOnlyList<ApprovalCardViewModel> Cards);
