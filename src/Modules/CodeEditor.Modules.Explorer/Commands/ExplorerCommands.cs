using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Explorer.Resources;
using CodeEditor.Modules.Explorer.ViewModels;

namespace CodeEditor.Modules.Explorer.Commands;

/// <summary>
/// Explorer commands with VS Code keybindings and the tree context menu. Commands act on the selected node;
/// <c>F2</c>, <c>Del</c> and <c>Enter</c> apply only when the tree is focused and no name is being edited.
/// </summary>
public sealed class ExplorerCommands(ExplorerViewModel explorer, ExplorerEditor editor) : IDisposable
{
    public const string ContextMenuId = "explorer.context";

    public const string NewFileId = "explorer.newFile";
    public const string NewFolderId = "explorer.newFolder";
    public const string OpenId = "explorer.open";
    public const string RenameId = "explorer.rename";
    public const string DeleteId = "explorer.delete";
    public const string CopyPathId = "explorer.copyPath";
    public const string CopyRelativePathId = "explorer.copyRelativePath";
    public const string RevealId = "explorer.revealInFileManager";
    public const string RefreshId = "explorer.refresh";
    public const string CollapseAllId = "explorer.collapseAll";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var workspaceOpen = ContextExpression.Parse(IWorkspace.OpenContextKey);
        var treeFocused = ContextExpression.Parse(
            $"{ExplorerViewModel.FocusContextKey} && !{ExplorerViewModel.EditingContextKey}");

        Add(commands, NewFileId, Strings.NewFile, async () => await editor.BeginCreateAsync(isDirectory: false), workspaceOpen);
        Add(commands, NewFolderId, Strings.NewFolder, async () => await editor.BeginCreateAsync(isDirectory: true), workspaceOpen);
        Add(commands, OpenId, Strings.Open, async () => await explorer.OpenAsync(null), workspaceOpen);
        Add(commands, RenameId, Strings.Rename, () => editor.BeginRename(), workspaceOpen);
        Add(commands, DeleteId, Strings.Delete, () => { editor.Delete(); }, workspaceOpen);
        Add(commands, CopyPathId, Strings.CopyPath, () => editor.CopyPath(relative: false), workspaceOpen);
        Add(commands, CopyRelativePathId, Strings.CopyRelativePath, () => editor.CopyPath(relative: true), workspaceOpen);
        Add(commands, RevealId, Strings.RevealInFileManager, editor.RevealInFileManager, workspaceOpen);
        Add(commands, RefreshId, Strings.Refresh, async () => await explorer.RefreshAsync(), workspaceOpen);
        Add(commands, CollapseAllId, Strings.CollapseAll, explorer.CollapseAll, workspaceOpen);

        Bind(keybindings, "Enter", OpenId, treeFocused);
        Bind(keybindings, "F2", RenameId, treeFocused);
        Bind(keybindings, "Delete", DeleteId, treeFocused);
        Bind(keybindings, "Shift+Alt+C", CopyPathId, null);
        Bind(keybindings, "Ctrl+K Ctrl+Shift+C", CopyRelativePathId, null);
        Bind(keybindings, "Shift+Alt+R", RevealId, null);

        RegisterContextMenu(menus);
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void RegisterContextMenu(IMenuRegistry menus)
    {
        var fileSelected = ContextExpression.Parse($"!{ExplorerViewModel.FolderSelectedContextKey}");
        (string Id, string Group, string Title, ContextExpression? When)[] items =
        [
            (NewFileId, "1_new", Strings.MenuNewFile, null),
            (NewFolderId, "1_new", Strings.MenuNewFolder, null),
            (OpenId, "2_open", Strings.MenuOpen, fileSelected),
            (RevealId, "3_reveal", Strings.MenuRevealInFileManager, null),
            (CopyPathId, "4_path", Strings.MenuCopyPath, null),
            (CopyRelativePathId, "4_path", Strings.MenuCopyRelativePath, null),
            (RenameId, "5_edit", Strings.MenuRename, null),
            (DeleteId, "5_edit", Strings.MenuDelete, null),
        ];

        for (var i = 0; i < items.Length; i++)
        {
            var (id, group, title, when) = items[i];
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(ContextMenuId, id, group, i, title, when)));
        }
    }

    private void Add(ICommandRegistry commands, string id, string title, Func<Task> handler, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (_, _) => await handler(), Strings.ModuleName, when)));

    private void Add(ICommandRegistry commands, string id, string title, Action handler, ContextExpression when) =>
        Add(commands, id, title, () =>
        {
            handler();
            return Task.CompletedTask;
        }, when);

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId, ContextExpression? when) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, when)));
}
