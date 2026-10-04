using CodeEditor.Core.Files;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Shell.Session;

/// <summary>
/// Captures the file tabs of the editor area and reopens them: each tab returns to its group (ADR 0031), files deleted
/// since are skipped. Views without a file (diffs, previews) are not kept.
/// </summary>
public sealed class FolderTabs(EditorAreaViewModel editors, IFileSystem fileSystem)
{
    public FolderSession Capture(string folder) => new()
    {
        Folder = folder,
        Tabs = [.. editors.Groups.SelectMany((group, index) => group.Tabs.Where(tab => tab.FilePath is not null).Select(tab => new SessionTab(tab.FilePath!, tab.IsPreview, index)))],
        ActiveTab = editors.Active?.FilePath,
    };

    public async Task RestoreAsync(FolderSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        EditorTab? active = null;
        foreach (var tab in session.Tabs.Where(tab => fileSystem.FileExists(tab.Path)))
        {
            editors.Activate(GroupFor(tab.Group));
            var opened = await editors.OpenAsync(new OpenFileRequest(tab.Path, tab.IsPreview));
            if (string.Equals(tab.Path, session.ActiveTab, StringComparison.OrdinalIgnoreCase))
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

    // Missing groups are recreated on the right; ones left empty by deleted files are closed afterwards.
    private EditorGroupViewModel GroupFor(int index)
    {
        while (editors.Groups.Count <= index && editors.AddGroup(editors.Groups[^1]) is not null)
        {
        }

        return editors.Groups[Math.Clamp(index, 0, editors.Groups.Count - 1)];
    }
}
