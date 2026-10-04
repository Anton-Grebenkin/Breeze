namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Outcome of a check before the turn ends: <paramref name="Prompt"/> is the message that continues the turn
/// (<c>null</c> ends it), <paramref name="Status"/> is the feed line.
/// </summary>
public sealed record TurnCheck(string? Prompt, string Status);
