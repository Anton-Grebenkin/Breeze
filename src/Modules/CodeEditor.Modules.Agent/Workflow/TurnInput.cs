using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>The message for the next model round: a user request, approval responses or a harness hint.</summary>
/// <param name="Continues">Continues an answer cut by the length limit: text is appended to the same feed answer.</param>
/// <param name="Revises">A check sent the model back to work: the previous feed summary was interim and becomes work log.</param>
public sealed record TurnInput(ChatMessage Message, bool Continues = false, bool Revises = false);
