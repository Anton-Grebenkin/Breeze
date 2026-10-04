using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.Layout;

/// <summary>
/// Persisted layout: tool window areas and the window. New fields are optional so older files still load.
/// </summary>
public sealed record LayoutState(
    ToolWindowAreaState? SideBar,
    ToolWindowAreaState? Panel,
    WindowPlacement? Window)
{
    /// <summary>The secondary side bar on the right; absent in older layouts.</summary>
    public ToolWindowAreaState? SecondarySideBar { get; init; }

    /// <summary>Tool windows moved away from their declared area: id → area name (ADR 0031).</summary>
    public Dictionary<string, string>? ToolWindowLocations { get; init; }
}
