using Microsoft.Agents.AI.Workflows;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>A model round ended: the feed closes the answer and helper-model usage is added to the chat's usage.</summary>
public sealed class ModelRoundFinishedEvent : WorkflowEvent;
