using CodeEditor.Shell.Menus;

namespace CodeEditor.Shell.Editors;

/// <summary>Editor area content for the view: tabs and the tab context menu.</summary>
public sealed class EditorAreaHost(EditorAreaViewModel editors, MenuViewModelFactory menus) : IDisposable
{
    public EditorAreaViewModel Editors { get; } = editors;

    public MenuViewModel TabContextMenu { get; } = menus.Create(EditorCommands.TabContextMenuId);

    public void Dispose() => TabContextMenu.Dispose();
}
