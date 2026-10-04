using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.Layout;

/// <summary>
/// Workbench layout: left and right side bars, bottom panel, window placement and tool window locations, which can be
/// moved between areas and into the editor area (ADR 0031). Loaded when the window is created (after all modules have
/// declared their tool windows) and saved on close.
/// </summary>
public sealed class WorkbenchLayout : IDisposable
{
    public const double DefaultSideBarWidth = 260;
    public const double MinSideBarWidth = 170;
    public const double DefaultPanelHeight = 220;
    public const double MinPanelHeight = 100;
    public const double DefaultSecondarySideBarWidth = 380;
    public const double MinSecondarySideBarWidth = 260;

    private readonly ILayoutStore _store;
    private readonly EditorToolWindows _editorWindows;

    public WorkbenchLayout(ToolWindowPlacement placement, EditorToolWindows editorWindows, ILayoutStore store)
    {
        ArgumentNullException.ThrowIfNull(placement);
        Placement = placement;
        _editorWindows = editorWindows;
        _store = store;
        SideBar = new ToolWindowAreaViewModel(ToolWindowLocation.SideBar, DefaultSideBarWidth, MinSideBarWidth, placement);
        Panel = new ToolWindowAreaViewModel(ToolWindowLocation.Panel, DefaultPanelHeight, MinPanelHeight, placement);
        SecondarySideBar = new ToolWindowAreaViewModel(
            ToolWindowLocation.SecondarySideBar, DefaultSecondarySideBarWidth, MinSecondarySideBarWidth, placement);
        Placement.MoveRequested += OnMoveRequested;
    }

    public ToolWindowAreaViewModel SideBar { get; }

    public ToolWindowAreaViewModel Panel { get; }

    /// <summary>Right of the documents; hosts the agent chat.</summary>
    public ToolWindowAreaViewModel SecondarySideBar { get; }

    /// <summary>Which area each tool window is in.</summary>
    public ToolWindowPlacement Placement { get; }

    /// <summary>Window placement from the saved layout; the view applies it before showing the window.</summary>
    public WindowPlacement? Window { get; set; }

    /// <exception cref="ArgumentOutOfRangeException">The editor area is not a tool window area.</exception>
    public ToolWindowAreaViewModel AreaOf(ToolWindowLocation location) =>
        location switch
        {
            ToolWindowLocation.SideBar => SideBar,
            ToolWindowLocation.SecondarySideBar => SecondarySideBar,
            ToolWindowLocation.Panel => Panel,
            _ => throw new ArgumentOutOfRangeException(nameof(location), location, "The editor area is not a tool window area."),
        };

    /// <summary>Shows the tool window where it currently is; <paramref name="focus"/> moves focus into its content.</summary>
    /// <returns><c>false</c> if there is no such tool window.</returns>
    public bool Show(string id, bool focus = true)
    {
        if (Placement.Find(id) is not { } toolWindow)
        {
            return false;
        }

        if (toolWindow.Location == ToolWindowLocation.Editor)
        {
            _editorWindows.Show(toolWindow);
        }
        else
        {
            AreaOf(toolWindow.Location).Show(id);
        }

        if (focus && toolWindow.Content is IFocusableContent content)
        {
            content.RequestFocus();
        }

        return true;
    }

    /// <summary>Moves a tool window to an area and shows it there.</summary>
    public void Move(string id, ToolWindowLocation location)
    {
        if (Placement.Move(id, location))
        {
            Show(id, focus: false);
        }
    }

    public void Load()
    {
        var state = _store.Load();
        if (state is null)
        {
            return;
        }

        // Locations first, so each area looks up its active tool window where it was moved to.
        Placement.Restore(state.ToolWindowLocations);
        if (state.SideBar is not null)
        {
            SideBar.Restore(state.SideBar);
        }

        if (state.Panel is not null)
        {
            Panel.Restore(state.Panel);
        }

        if (state.SecondarySideBar is not null)
        {
            SecondarySideBar.Restore(state.SecondarySideBar);
        }

        Window = state.Window;
    }

    public void Save() => _store.Save(new LayoutState(SideBar.Capture(), Panel.Capture(), Window)
    {
        SecondarySideBar = SecondarySideBar.Capture(),
        ToolWindowLocations = Placement.Capture(),
    });

    public void Dispose()
    {
        Placement.MoveRequested -= OnMoveRequested;
        SideBar.Dispose();
        Panel.Dispose();
        SecondarySideBar.Dispose();
    }

    private void OnMoveRequested(object? sender, ToolWindowMoveRequest request) => Move(request.Id, request.Location);
}
