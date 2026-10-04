using CodeEditor.Shell.Resources;

namespace CodeEditor.Shell.ToolWindows;

/// <summary>Area names for the "Move to…" menus and the palette picker (ADR 0031).</summary>
public static class ToolWindowLocations
{
    /// <summary>Areas in screen order: left to right, then the bottom panel.</summary>
    public static IReadOnlyList<ToolWindowLocation> All { get; } =
        [ToolWindowLocation.SideBar, ToolWindowLocation.Editor, ToolWindowLocation.SecondarySideBar, ToolWindowLocation.Panel];

    public static string Title(ToolWindowLocation location) => location switch
    {
        ToolWindowLocation.SideBar => Strings.SideBar,
        ToolWindowLocation.SecondarySideBar => Strings.SecondarySideBar,
        ToolWindowLocation.Editor => Strings.EditorArea,
        _ => Strings.Panel,
    };

    /// <summary>Menu item title, e.g. "Move to Secondary Side Bar".</summary>
    public static string MoveTitle(ToolWindowLocation location) => location switch
    {
        ToolWindowLocation.SideBar => Strings.MoveToSideBar,
        ToolWindowLocation.SecondarySideBar => Strings.MoveToSecondarySideBar,
        ToolWindowLocation.Editor => Strings.MoveToEditor,
        _ => Strings.MoveToPanel,
    };
}
