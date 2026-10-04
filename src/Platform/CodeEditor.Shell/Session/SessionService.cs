using CodeEditor.Core.Files;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Workspace;

namespace CodeEditor.Shell.Session;

/// <summary>
/// Session restore, as in VS Code: without arguments the previous folder opens with its tabs; with a folder on the
/// command line that folder opens (with tabs only if it is the same folder). Recent commands and files survive
/// restarts. The folder opens before the window is created, tabs after the first frame, to keep startup fast.
/// </summary>
public sealed class SessionService(
    ISessionStore store,
    IFileSystem fileSystem,
    IWorkspace workspace,
    WorkspaceCommands workspaceCommands,
    EditorAreaViewModel editors,
    RecentCommands recentCommands,
    RecentFiles recentFiles)
{
    private SessionState? _pendingTabs;

    /// <summary>Loads the state and opens the folder from the argument or the previous session.</summary>
    public void RestoreFolder(string? folderArgument)
    {
        var state = store.Load();
        recentCommands.Restore(state?.RecentCommands ?? []);
        recentFiles.Restore(state?.RecentFiles ?? []);

        // A vanished previous folder silently acts as a first run; errors are shown only for an explicit argument.
        var folder = folderArgument ?? (state?.Folder is { } last && fileSystem.DirectoryExists(last) ? last : null);
        if (folder is null || !workspaceCommands.TryOpen(folder))
        {
            return;
        }

        if (state?.Folder is { } saved && string.Equals(Path.GetFullPath(saved), workspace.Root, StringComparison.OrdinalIgnoreCase))
        {
            _pendingTabs = state;
        }
    }

    /// <summary>Reopens the previous session's tabs, skipping files deleted since.</summary>
    public async Task RestoreTabsAsync()
    {
        if (_pendingTabs is not { } state)
        {
            return;
        }

        _pendingTabs = null;
        EditorTab? active = null;
        foreach (var tab in state.Tabs.Where(tab => fileSystem.FileExists(tab.Path)))
        {
            editors.Activate(GroupFor(tab.Group));
            var opened = await editors.OpenAsync(new OpenFileRequest(tab.Path, tab.IsPreview));
            if (string.Equals(tab.Path, state.ActiveTab, StringComparison.OrdinalIgnoreCase))
            {
                active = opened;
            }
        }

        editors.CloseEmptyGroups();
        if (active is not null)
        {
            editors.Activate(active);
        }
    }

    /// <summary>Saves a snapshot on exit.</summary>
    public void Save() => store.Save(new SessionState
    {
        Folder = workspace.Root,
        Tabs = [.. editors.Groups.SelectMany((group, index) => group.Tabs.Where(tab => tab.FilePath is not null).Select(tab => new SessionTab(tab.FilePath!, tab.IsPreview, index)))],
        ActiveTab = editors.Active?.FilePath,
        RecentCommands = [.. recentCommands.Items],
        RecentFiles = [.. recentFiles.Items],
    });

    // Missing groups are recreated on the right; ones left empty by deleted files are closed afterwards.
    private EditorGroupViewModel GroupFor(int index)
    {
        while (editors.Groups.Count <= index && editors.AddGroup(editors.Groups[^1]) is not null)
        {
        }

        return editors.Groups[Math.Clamp(index, 0, editors.Groups.Count - 1)];
    }
}
