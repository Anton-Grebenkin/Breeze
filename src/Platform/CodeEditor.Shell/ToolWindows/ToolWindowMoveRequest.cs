namespace CodeEditor.Shell.ToolWindows;

/// <summary>A request to move a tool window (<see cref="ToolWindowPlacement.MoveRequested"/>).</summary>
public sealed record ToolWindowMoveRequest(string Id, ToolWindowLocation Location);
