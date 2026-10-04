using Microsoft.Agents.AI.Workflows;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>A queued message reached the model (ADR 0026): the feed shows it as a question at this point of the turn.</summary>
public sealed class UserMessageDeliveredEvent(QueuedMessage message) : WorkflowEvent
{
    public QueuedMessage Message { get; } = message;
}
