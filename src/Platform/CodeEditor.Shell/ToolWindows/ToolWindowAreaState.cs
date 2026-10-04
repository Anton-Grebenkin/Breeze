namespace CodeEditor.Shell.ToolWindows;

/// <summary>Persisted area state: visibility, size and active tool window.</summary>
public sealed record ToolWindowAreaState(bool IsVisible, double Size, string? ActiveId);
