using Microsoft.Agents.AI.Workflows;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>
/// The agent turn graph on Agent Framework Workflows (ADR 0013): model → approvals ⇄ user port → model;
/// model → checks → model or exit. Nodes do not touch the feed: it listens to graph events, and user decisions arrive
/// through the request port. The graph is built per turn because nodes hold turn state (counters, pending cards).
/// </summary>
public static class AgentTurnWorkflow
{
    public const string Name = "agent-turn";

    /// <summary>Port for the user's decisions on approval cards.</summary>
    public const string ApprovalPortId = "approve";

    public static Microsoft.Agents.AI.Workflows.Workflow Build(ModelExecutor model, ApprovalExecutor approvals, ChecksExecutor checks)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(checks);
        var port = RequestPort.Create<ApprovalPending, ApprovalDecided>(ApprovalPortId);
        var builder = new WorkflowBuilder(model);
        builder.AddEdge<ModelOutcome>(model, approvals, outcome => outcome?.NeedsApproval == true);
        builder.AddEdge<ModelOutcome>(model, checks, outcome => outcome?.NeedsApproval != true);
        builder.AddEdge(approvals, port);
        builder.AddEdge(port, approvals);
        builder.AddEdge(approvals, model);
        builder.AddEdge(checks, model);
        builder.WithOutputFrom(checks);
        return builder.WithName(Name).Build();
    }

    /// <summary>The graph as Mermaid, for documentation and the structure test.</summary>
    public static string Mermaid(Microsoft.Agents.AI.Workflows.Workflow workflow) => WorkflowVisualizer.ToMermaidString(workflow);
}
