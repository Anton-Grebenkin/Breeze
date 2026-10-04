using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// File and tab commands with VS Code keybindings: open, save, close, reopen closed, switch. Items go to the File menu
/// and the tab context menu.
/// </summary>
public sealed class EditorCommands(EditorAreaViewModel editors, IFileDialogs fileDialogs, ISystemShell systemShell) : IDisposable
{
    public const string TabContextMenuId = "editor.tab.context";

    public const string SaveId = "workbench.file.save";
    public const string SaveAllId = "workbench.file.saveAll";
    public const string RevertId = "workbench.file.revert";
    public const string CloseId = "workbench.editor.close";
    public const string CloseOthersId = "workbench.editor.closeOthers";
    public const string CloseToTheRightId = "workbench.editor.closeToTheRight";
    public const string CloseAllId = "workbench.editor.closeAll";
    public const string ReopenClosedId = "workbench.editor.reopenClosed";
    public const string NextId = "workbench.editor.next";
    public const string PreviousId = "workbench.editor.previous";
    public const string PreviousRecentId = "workbench.editor.previousRecent";
    public const string CopyPathId = "workbench.editor.copyPath";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var editorOpen = ContextExpression.Parse(EditorAreaViewModel.EditorOpenContextKey);
        var activeDirty = ContextExpression.Parse(EditorAreaViewModel.ActiveDirtyContextKey);
        var file = Strings.CategoryFile;
        var tabs = Strings.CategoryTabs;

        Add(commands, ShellCommandIds.OpenFile, Strings.OpenFile, file, OpenAsync, null);
        Add(commands, SaveId, Strings.Save, file, _ => editors.SaveActiveAsync(), editorOpen);
        Add(commands, SaveAllId, Strings.SaveAll, file, _ => editors.SaveAllAsync(), editorOpen);
        Add(commands, RevertId, Strings.RevertFile, file, _ => editors.RevertActiveAsync(), activeDirty);
        Add(commands, CloseId, Strings.Close, tabs, _ => editors.CloseAsync(null), editorOpen);
        Add(commands, CloseOthersId, Strings.CloseOthers, tabs, _ => editors.CloseOthersAsync(), editorOpen);
        Add(commands, CloseToTheRightId, Strings.CloseToTheRight, tabs, _ => editors.CloseToTheRightAsync(), editorOpen);
        Add(commands, CloseAllId, Strings.CloseAll, tabs, _ => editors.CloseAllAsync(), editorOpen);
        Add(commands, ReopenClosedId, Strings.ReopenClosedTab, tabs, _ => editors.ReopenClosedAsync(), null);
        Add(commands, NextId, Strings.NextTab, tabs, _ => Run(() => editors.ActivateNeighbor(1)), editorOpen);
        Add(commands, PreviousId, Strings.PreviousTab, tabs, _ => Run(() => editors.ActivateNeighbor(-1)), editorOpen);
        Add(commands, PreviousRecentId, Strings.RecentTab, tabs, _ => Run(editors.ActivatePreviousRecent), editorOpen);
        Add(commands, CopyPathId, Strings.CopyActivePath, tabs, _ => Run(CopyActivePath), editorOpen);

        Bind(keybindings, "Ctrl+O", ShellCommandIds.OpenFile);
        Bind(keybindings, "Ctrl+S", SaveId);
        Bind(keybindings, "Ctrl+K S", SaveAllId);
        Bind(keybindings, "Ctrl+F4", CloseId);
        Bind(keybindings, "Ctrl+W", CloseId);
        Bind(keybindings, "Ctrl+K Ctrl+W", CloseAllId);
        Bind(keybindings, "Ctrl+Shift+T", ReopenClosedId);
        Bind(keybindings, "Ctrl+PageDown", NextId);
        Bind(keybindings, "Ctrl+PageUp", PreviousId);
        Bind(keybindings, "Ctrl+Tab", PreviousRecentId);

        RegisterMenus(menus);
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void RegisterMenus(IMenuRegistry menus)
    {
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, ShellCommandIds.OpenFile, "1_open", order: 0, title: Strings.OpenFileMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, SaveId, "2_save", order: 1, title: Strings.SaveMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, SaveAllId, "2_save", order: 2, title: Strings.SaveAllMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, RevertId, "2_save", order: 3, title: Strings.RevertFileMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, CloseId, "3_close", order: 1, title: Strings.CloseEditorMenu)));

        (string Id, string Group, string Title)[] tabItems =
        [
            (CloseId, "1_close", Strings.CloseMenu),
            (CloseOthersId, "1_close", Strings.CloseOthersMenu),
            (CloseToTheRightId, "1_close", Strings.CloseToTheRightMenu),
            (CloseAllId, "1_close", Strings.CloseAllMenu),
            (CopyPathId, "2_path", Strings.CopyPathMenu),
        ];
        for (var i = 0; i < tabItems.Length; i++)
        {
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(TabContextMenuId, tabItems[i].Id, tabItems[i].Group, i, tabItems[i].Title)));
        }
    }

    private async Task OpenAsync(object? argument)
    {
        var request = argument switch
        {
            OpenFileRequest open => open,
            string path => new OpenFileRequest(path),
            _ => fileDialogs.PickFile(Strings.OpenFileDialogTitle) is { } picked ? new OpenFileRequest(picked) : null,
        };

        if (request is not null)
        {
            await editors.OpenAsync(request);
        }
    }

    private void CopyActivePath()
    {
        if (editors.Active?.FilePath is { } path)
        {
            systemShell.CopyToClipboard(path);
        }
    }

    private static Task Run(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private void Add(ICommandRegistry commands, string id, string title, string category, Func<object?, Task> handler, ContextExpression? when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (argument, _) => await handler(argument), category, when)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId)));
}
