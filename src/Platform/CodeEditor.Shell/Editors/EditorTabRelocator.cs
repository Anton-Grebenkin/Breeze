using CodeEditor.Core.Documents;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Open tabs follow a file or folder that was moved or renamed, as in VS Code: each file tab under the old path reopens
/// at the new path in the same group and position, keeping its preview state; unsaved edits carry over as one undo
/// step. Module views that merely show a file (a diagram preview) are left as they are.
/// </summary>
public sealed class EditorTabRelocator(EditorAreaViewModel area)
{
    public async Task FollowAsync(string oldPath, string newPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        // A case-only rename keeps the tab: paths compare case-insensitively, so the old tab is the new one.
        var moved = area.Tabs.Where(tab => IsFileTabUnder(tab, oldPath)).ToList();
        if (moved.Count == 0 || string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var active = area.Active;
        foreach (var tab in moved)
        {
            var replacement = await ReopenAsync(tab, newPath + tab.FilePath![oldPath.Length..]);
            if (ReferenceEquals(active, tab))
            {
                active = replacement ?? tab;
            }
        }

        area.Activate(active);
    }

    // A file tab is keyed by its path; a module view showing a file has its own id.
    private static bool IsFileTabUnder(EditorTab tab, string folderOrFile) =>
        tab.FilePath is { } path
        && string.Equals(tab.Key, path, StringComparison.OrdinalIgnoreCase)
        && (string.Equals(path, folderOrFile, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(folderOrFile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    /// <returns>The new tab, or <c>null</c> if the file couldn't be opened at its new path (the old tab stays).</returns>
    private async Task<EditorTab?> ReopenAsync(EditorTab tab, string path)
    {
        if (area.GroupOf(tab) is not { } group)
        {
            return null;
        }

        var shown = group.Active;
        area.Activate(group);
        var request = new OpenFileRequest(path, tab.IsPreview);
        var replacement = tab is EditorTabViewModel ? await area.OpenTextAsync(request) : await area.OpenAsync(request);
        if (replacement is null || ReferenceEquals(replacement, tab))
        {
            return replacement;
        }

        if (tab is EditorTabViewModel { IsDirty: true } edited && replacement is EditorTabViewModel text)
        {
            CarryEdits(edited.Document, text.Document);
        }

        // A preview replacement has already taken the old tab's place; otherwise it goes right before the old tab.
        if (group.Tabs.Contains(tab))
        {
            area.MoveToGroup(replacement, group, group.Tabs.IndexOf(tab));
            await area.CloseAsync(tab);
        }

        if (shown is not null && !ReferenceEquals(shown, tab) && group.Tabs.Contains(shown))
        {
            area.Activate(shown);
        }

        return replacement;
    }

    // The edits now live in the new document, so the old one closes without the save prompt.
    private static void CarryEdits(IDocument from, IDocument to)
    {
        to.Buffer.Replace(0, to.Buffer.Length, from.Buffer.GetText());
        from.Buffer.MarkSaved();
    }
}
