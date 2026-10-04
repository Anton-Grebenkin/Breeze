using CodeEditor.Modules.Agent.Contracts.Approvals;

namespace CodeEditor.Modules.Diagrams.Services.Agent;

/// <summary>
/// Approval policy for the <c>diagram</c> tool (ADR 0035). <c>check</c> and <c>view</c> change nothing and need no
/// approval. <c>render</c> writes an image next to a diagram the agent already wrote in front of the user: a new file or
/// its own earlier export needs no approval, but a same-named file not written by the diagram export needs a card
/// (<see cref="DiagramRenderTargets"/>), since it may be a human-drawn image. The scout and the critic only get
/// <c>check</c>: there is nobody to show them images, and they cannot write files.
/// </summary>
public sealed class DiagramApprovals(DiagramRenderTargets targets) : IAgentApprovalPolicy
{
    public bool CanDecide(string toolName) => toolName == DiagramAgentTools.ToolName;

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && (Action(arguments) != DiagramAgentTools.Render || targets.Foreign(arguments).Count == 0);

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Action(arguments) == DiagramAgentTools.Check;

    /// <summary>No "Always allow": a card appears only for a foreign file, which is decided each time.</summary>
    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) => null;

    public void AllowAlways(string toolName, string rule)
    {
    }

    private static string? Action(IDictionary<string, object?> arguments) =>
        arguments.TryGetValue("action", out var value) ? value?.ToString() : null;
}
