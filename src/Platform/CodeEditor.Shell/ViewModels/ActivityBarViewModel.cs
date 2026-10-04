using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.ViewModels;

/// <summary>
/// The activity bar on the left: side bar tool window icons and Manage at the bottom.
/// </summary>
public sealed class ActivityBarViewModel(WorkbenchLayout layout, MenuViewModelFactory menus) : IDisposable
{
    /// <summary>The side bar; its tool windows are shown as icons that toggle on click.</summary>
    public ToolWindowAreaViewModel SideBar { get; } = layout.SideBar;

    /// <summary>The Manage button menu: palette, theme, settings, keybindings.</summary>
    public MenuViewModel Manage { get; } = menus.Create(MenuIds.Manage);

    public void Dispose() => Manage.Dispose();
}
