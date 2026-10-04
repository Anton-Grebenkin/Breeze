using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Session;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Workspace;

/// <summary>
/// Opens and closes the window's folder, as VS Code does: tabs belong to the folder, so leaving it asks about unsaved
/// files, saves its tabs and closes them, and the next folder reopens its own tabs.
/// </summary>
public sealed class WorkspaceSwitcher(
    IWorkspace workspace,
    IFileSystem fileSystem,
    RecentFolders recent,
    StatusBarViewModel statusBar,
    EditorAreaViewModel editors,
    ISessionStore sessions,
    FolderTabs tabs)
{
    /// <summary>Opens a folder without touching tabs (startup); a missing folder is reported and leaves the recent list.</summary>
    public bool TryOpen(string folder)
    {
        try
        {
            workspace.Open(folder);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            ReportMissing(folder);
            return false;
        }
    }

    /// <summary>Switches the window to a folder. <c>false</c> if the folder is missing or the user kept unsaved files.</summary>
    public async Task<bool> OpenAsync(string folder)
    {
        if (IsOpen(folder))
        {
            return true;
        }

        if (!fileSystem.DirectoryExists(folder))
        {
            ReportMissing(folder);
            return false;
        }

        if (!await LeaveCurrentAsync() || !TryOpen(folder))
        {
            return false;
        }

        await RestoreTabsAsync();
        return true;
    }

    /// <summary>Closes the folder and its tabs; <c>false</c> if the user kept unsaved files.</summary>
    public async Task<bool> CloseAsync()
    {
        if (!await LeaveCurrentAsync())
        {
            return false;
        }

        workspace.Close();
        return true;
    }

    /// <summary>Reopens the tabs saved for the open folder; <paramref name="fallback"/> when none are saved.</summary>
    public Task RestoreTabsAsync(FolderSession? fallback = null) =>
        workspace.Root is { } root && (sessions.LoadFolder(root) ?? fallback) is { } saved ? tabs.RestoreAsync(saved) : Task.CompletedTask;

    /// <summary>Saves the tabs of the open folder (on exit).</summary>
    public void SaveCurrent()
    {
        if (workspace.Root is { } root)
        {
            sessions.SaveFolder(tabs.Capture(root));
        }
    }

    public bool IsOpen(string folder) =>
        workspace.Root is { } root && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)), root, StringComparison.OrdinalIgnoreCase);

    // The snapshot is taken before closing and saved only if the user didn't cancel.
    private async Task<bool> LeaveCurrentAsync()
    {
        var snapshot = workspace.Root is { } root ? tabs.Capture(root) : null;
        if (!await editors.CloseAllAsync())
        {
            return false;
        }

        if (snapshot is not null)
        {
            sessions.SaveFolder(snapshot);
        }

        return true;
    }

    private void ReportMissing(string folder)
    {
        recent.Remove(folder);
        statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.FolderNotFound, folder);
    }
}
