namespace CodeEditor.Agent.Eval;

/// <summary>Outcome of checking a task.</summary>
/// <param name="Problems">Empty when the task is solved; otherwise the reasons.</param>
/// <param name="Score">Share of hidden tests passed, 0 to 1; <c>null</c> when the task has none.</param>
internal sealed record EvalVerdict(IReadOnlyList<string> Problems, double? Score);
