using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Tests.Editors;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.Tests.Infrastructure;

/// <summary>Test layout: tool window registry and placement, an in-memory editor area and tool window tabs.</summary>
internal sealed class LayoutFixture : IDisposable
{
    public LayoutFixture(ILayoutStore store, ToolWindowRegistry? registry = null)
    {
        Registry = registry ?? new ToolWindowRegistry();
        Placement = new ToolWindowPlacement(Registry, Shell.Keybindings);
        EditorWindows = new EditorToolWindows(Editors.Area, Placement, Editors.Context);
        Layout = new WorkbenchLayout(Placement, EditorWindows, store);
    }

    public ShellFixture Shell { get; } = new();

    public EditorAreaFixture Editors { get; } = new();

    public ToolWindowRegistry Registry { get; }

    public ToolWindowPlacement Placement { get; }

    public EditorToolWindows EditorWindows { get; }

    public WorkbenchLayout Layout { get; }

    public void Dispose()
    {
        Layout.Dispose();
        EditorWindows.Dispose();
        Placement.Dispose();
        Editors.Dispose();
    }
}
