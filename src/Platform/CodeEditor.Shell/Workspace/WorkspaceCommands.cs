using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.Workspace;

/// <summary>
/// Window and folder commands with File menu items: new window (<c>Ctrl+Shift+N</c>), open folder here
/// (<c>Ctrl+K Ctrl+O</c>) or in a new window, open recent, close. Switching goes through
/// <see cref="WorkspaceSwitcher"/>, which closes the old folder's tabs. The Open Recent submenu is rebuilt when the
/// list changes.
/// </summary>
public sealed class WorkspaceCommands(
    IWorkspace workspace,
    WorkspaceSwitcher switcher,
    RecentFolders recent,
    IFileDialogs fileDialogs,
    IAppWindows windows) : IDisposable
{
    public const string NewWindowId = "workbench.newWindow";
    public const string OpenFolderInNewWindowId = "workbench.folder.openInNewWindow";
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
        _registrations.Add(commands.Register(new CommandDefinition(NewWindowId, Strings.NewWindow, NewWindow, category)));
        _registrations.Add(commands.Register(new CommandDefinition(OpenFolderId, Strings.OpenFolder, OpenFolder, category)));
        _registrations.Add(commands.Register(new CommandDefinition(OpenFolderInNewWindowId, Strings.OpenFolderInNewWindow, OpenFolderInNewWindow, category)));
        _registrations.Add(commands.Register(new CommandDefinition(CloseFolderId, Strings.CloseFolder, CloseFolder, category, workspaceOpen)));
        _registrations.Add(commands.Register(new CommandDefinition(OpenRecentId, Strings.OpenRecentFolder, OpenRecent, category)));
        _registrations.Add(commands.Register(new CommandDefinition(ClearRecentId, Strings.ClearRecentFolders, ClearRecent, category)));
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+K Ctrl+O"), OpenFolderId)));
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+Shift+N"), NewWindowId)));

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, NewWindowId, OpenGroup, order: 0, title: Strings.NewWindowMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, OpenFolderId, OpenGroup, order: 1, title: Strings.OpenFolderMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, OpenFolderInNewWindowId, OpenGroup, order: 2, title: Strings.OpenFolderInNewWindowMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.File, RecentMenuId, Strings.OpenRecentMenu, OpenGroup, order: 3)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, CloseFolderId, OpenGroup, order: 4, title: Strings.CloseFolderMenu)));

        recent.Changed += OnRecentChanged;
        workspace.Changed += OnWorkspaceChanged;
        RebuildRecentMenu();
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

    private async ValueTask OpenFolder(object? argument, CancellationToken cancellationToken)
    {
        if ((argument as string ?? fileDialogs.PickFolder(Strings.OpenFolderDialogTitle)) is { } folder)
        {
            await switcher.OpenAsync(folder);
        }
    }

    private ValueTask NewWindow(object? argument, CancellationToken cancellationToken)
    {
        windows.OpenNew(argument as string);
        return ValueTask.CompletedTask;
    }

    // A folder already open in another window brings it to the front, as in VS Code.
    private async ValueTask OpenFolderInNewWindow(object? argument, CancellationToken cancellationToken)
    {
        if ((argument as string ?? fileDialogs.PickFolder(Strings.OpenFolderDialogTitle)) is { } folder && !await windows.TryActivateAsync(folder))
        {
            windows.OpenNew(folder);
        }
    }

    private async ValueTask OpenRecent(object? argument, CancellationToken cancellationToken)
    {
        if (argument is string folder)
        {
            await switcher.OpenAsync(folder);
        }
    }

    private async ValueTask CloseFolder(object? argument, CancellationToken cancellationToken) => await switcher.CloseAsync();

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
