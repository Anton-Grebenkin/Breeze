using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Workspace;

/// <summary>
/// Workspace folder commands with File menu items: open (<c>Ctrl+K Ctrl+O</c>), close, open recent. The Open Recent
/// submenu is rebuilt when the list changes.
/// </summary>
public sealed class WorkspaceCommands(
    IWorkspace workspace,
    RecentFolders recent,
    IFileDialogs fileDialogs,
    StatusBarViewModel statusBar) : IDisposable
{
    public const string OpenFolderId = "workbench.folder.open";
    public const string CloseFolderId = "workbench.folder.close";
    public const string OpenRecentId = "workbench.folder.openRecent";
    public const string ClearRecentId = "workbench.folder.clearRecent";
    public const string RecentMenuId = "menubar.file.recent";

    private const string OpenGroup = "1_open";
    private const string RecentItemsGroup = "1_items";
    private const string RecentClearGroup = "2_clear";

    private readonly List<IDisposable> _registrations = [];
    private readonly List<IDisposable> _recentItems = [];
    private IMenuRegistry? _menus;

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        _menus = menus;
        var workspaceOpen = ContextExpression.Parse(IWorkspace.OpenContextKey);

        var category = Strings.CategoryFile;
        _registrations.Add(commands.Register(new CommandDefinition(OpenFolderId, Strings.OpenFolder, OpenFolder, category)));
        _registrations.Add(commands.Register(new CommandDefinition(CloseFolderId, Strings.CloseFolder, CloseFolder, category, workspaceOpen)));
        _registrations.Add(commands.Register(new CommandDefinition(OpenRecentId, Strings.OpenRecentFolder, OpenRecent, category)));
        _registrations.Add(commands.Register(new CommandDefinition(ClearRecentId, Strings.ClearRecentFolders, ClearRecent, category)));
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+K Ctrl+O"), OpenFolderId)));

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, OpenFolderId, OpenGroup, order: 1, title: Strings.OpenFolderMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.File, RecentMenuId, Strings.OpenRecentMenu, OpenGroup, order: 2)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, CloseFolderId, OpenGroup, order: 3, title: Strings.CloseFolderMenu)));

        recent.Changed += OnRecentChanged;
        workspace.Changed += OnWorkspaceChanged;
        RebuildRecentMenu();
    }

    /// <summary>Opens a folder and records it as recent; a missing folder is removed from the list.</summary>
    public bool TryOpen(string folder)
    {
        try
        {
            workspace.Open(folder);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            recent.Remove(folder);
            statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.FolderNotFound, folder);
            return false;
        }
    }

    public void Dispose()
    {
        recent.Changed -= OnRecentChanged;
        workspace.Changed -= OnWorkspaceChanged;
        foreach (var registration in _registrations.Concat(_recentItems))
        {
            registration.Dispose();
        }

        _registrations.Clear();
        _recentItems.Clear();
    }

    private ValueTask OpenFolder(object? argument, CancellationToken cancellationToken)
    {
        var folder = argument as string ?? fileDialogs.PickFolder(Strings.OpenFolderDialogTitle);
        if (folder is not null)
        {
            TryOpen(folder);
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask OpenRecent(object? argument, CancellationToken cancellationToken)
    {
        if (argument is string folder)
        {
            TryOpen(folder);
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask CloseFolder(object? argument, CancellationToken cancellationToken)
    {
        workspace.Close();
        return ValueTask.CompletedTask;
    }

    private ValueTask ClearRecent(object? argument, CancellationToken cancellationToken)
    {
        recent.Clear();
        return ValueTask.CompletedTask;
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        if (workspace.Root is { } root)
        {
            recent.Add(root);
        }
    }

    private void OnRecentChanged(object? sender, EventArgs e) => RebuildRecentMenu();

    private void RebuildRecentMenu()
    {
        _recentItems.ForEach(item => item.Dispose());
        _recentItems.Clear();

        for (var i = 0; i < recent.Items.Count; i++)
        {
            _recentItems.Add(_menus!.Register(MenuItemDefinition.ForCommand(
                RecentMenuId, OpenRecentId, RecentItemsGroup, order: i, title: EscapeAccessKey(recent.Items[i]), argument: recent.Items[i])));
        }

        if (recent.Items.Count > 0)
        {
            _recentItems.Add(_menus!.Register(MenuItemDefinition.ForCommand(RecentMenuId, ClearRecentId, RecentClearGroup, title: Strings.ClearRecentMenu)));
        }
    }

    /// <summary>"_" in a path is not an access key; WPF escapes it by doubling.</summary>
    private static string EscapeAccessKey(string text) => text.Replace("_", "__", StringComparison.Ordinal);
}
