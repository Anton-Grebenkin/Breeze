using Microsoft.Agents.AI.Workflows;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>
/// A model round started: the feed opens an answer or continues a truncated one (<see cref="Continues"/>); after a
/// check (<see cref="Revises"/>) the previous summary becomes part of the work log.
/// </summary>
public sealed class ModelRoundStartedEvent(bool continues, bool revises = false) : WorkflowEvent
{
    public bool Continues { get; } = continues;

    public bool Revises { get; } = revises;
}
