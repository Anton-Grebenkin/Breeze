namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>Graph output: the turn ended.</summary>
/// <param name="ReachedStepLimit">The summary is interim: the mode's request limit was reached.</param>
public sealed record TurnResult(bool ReachedStepLimit);
