namespace CodeEditor.Modules.Agent.Services.Watchdog;

/// <summary>A finished tool call as the turn observers see it (<see cref="ToolCallObservers"/>).</summary>
/// <param name="Result">The text the model gets; <c>null</c> for a non-text result.</param>
/// <param name="Failed">The tool reported an error (the result starts with the error prefix).</param>
public sealed record ToolCallRecord(string Tool, IReadOnlyDictionary<string, object?> Arguments, string? Result, bool Failed, TimeSpan Elapsed);
