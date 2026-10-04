using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>A fragment of the model's answer for the feed while the round runs.</summary>
public sealed class ModelUpdateEvent(AgentResponseUpdate update) : WorkflowEvent
{
    public AgentResponseUpdate Update { get; } = update;
}
