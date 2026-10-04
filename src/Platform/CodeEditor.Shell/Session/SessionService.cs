using CodeEditor.Core.Files;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Workspace;

namespace CodeEditor.Shell.Session;

/// <summary>
/// Session restore, as in VS Code: without arguments the previous folder opens, with a folder on the command line that
/// folder opens; either way with the tabs saved for it. Recent commands and files survive restarts. The folder opens
/// before the window is created, tabs after the first frame, to keep startup fast.
/// </summary>
public sealed class SessionService(
    ISessionStore store,
    IFileSystem fileSystem,
    IWorkspace workspace,
    WorkspaceSwitcher switcher,
    RecentCommands recentCommands,
    RecentFiles recentFiles)
{
    private bool _tabsPending;
    private FolderSession? _legacyTabs;

    /// <summary>Loads the state and opens the folder from the argument or the previous session.</summary>
    public void RestoreFolder(string? folderArgument)
    {
        var state = store.Load();
        recentCommands.Restore(state?.RecentCommands ?? []);
        recentFiles.Restore(state?.RecentFiles ?? []);

        // A vanished previous folder silently acts as a first run; errors are shown only for an explicit argument.
        var folder = folderArgument ?? (state?.Folder is { } last && fileSystem.DirectoryExists(last) ? last : null);
        if (folder is null || !switcher.TryOpen(folder))
        {
            return;
        }

        _tabsPending = true;
        _legacyTabs = LegacyTabs(state);
    }

    /// <summary>Reopens the tabs of the folder opened by <see cref="RestoreFolder"/>.</summary>
    public async Task RestoreTabsAsync()
    {
        if (!_tabsPending)
        {
            return;
        }

        _tabsPending = false;
        await switcher.RestoreTabsAsync(_legacyTabs);
        _legacyTabs = null;
    }

    /// <summary>Saves a snapshot on exit.</summary>
    public void Save()
    {
        switcher.SaveCurrent();
        store.Save(new SessionState
        {
            Folder = workspace.Root,
            RecentCommands = [.. recentCommands.Items],
            RecentFiles = [.. recentFiles.Items],
        });
    }

    // Before tabs were kept per folder, state.json held the tabs of the last folder: they are used once.
    private FolderSession? LegacyTabs(SessionState? state) =>
        state is { Folder: { } folder, Tabs.Count: > 0 } && switcher.IsOpen(folder)
            ? new FolderSession { Folder = folder, Tabs = state.Tabs, ActiveTab = state.ActiveTab }
            : null;
}
