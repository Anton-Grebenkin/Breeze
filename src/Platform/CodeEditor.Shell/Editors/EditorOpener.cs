namespace CodeEditor.Shell.Editors;

/// <summary>
/// Opens a file in a tab of the active group, with a viewer or as text (<see cref="EditorTabFactory"/>). An open file is
/// activated; a repeated request while the file is loading (a double click is two clicks) waits for the first one
/// instead of creating a second tab. Opening as text a file shown by a text-format viewer (CSV as a table) replaces
/// the tab in place.
/// </summary>
internal sealed class EditorOpener(EditorAreaViewModel area, EditorTabFactory factory)
{
    private readonly Dictionary<string, Task<EditorTab?>> _opening = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="asText">Open as text for editing, bypassing viewers.</param>
    public async Task<EditorTab?> OpenAsync(OpenFileRequest request, bool asText)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = Path.GetFullPath(request.FilePath);
        if (_opening.TryGetValue(path, out var pending))
        {
            await pending;
        }

        if (area.Find(path) is { } existing)
        {
            if (asText && existing is not EditorTabViewModel && factory.IsTextViewer(path))
            {
                return await ReplaceWithTextAsync(existing, request);
            }

            existing.IsPreview &= request.Preview;
            area.Activate(existing);
            return existing;
        }

        var viewer = asText ? null : factory.ViewerFor(path);
        var opening = viewer is not null
            ? Task.FromResult<EditorTab?>(area.Insert(EditorTabFactory.CreateViewer(viewer, path, request.Preview)))
            : OpenDocumentAsync(request, allowFallback: !asText);
        _opening[path] = opening;
        try
        {
            return await opening;
        }
        finally
        {
            _opening.Remove(path);
        }
    }

    private async Task<EditorTab?> OpenDocumentAsync(OpenFileRequest request, bool allowFallback) =>
        await factory.CreateDocumentAsync(request, allowFallback) is { } tab ? area.Insert(tab) : null;

    // The text tab joins the viewer's group before the viewer closes, so the group never empties and disappears.
    private async Task<EditorTab?> ReplaceWithTextAsync(EditorTab viewer, OpenFileRequest request)
    {
        if (area.GroupOf(viewer) is not { } group || await factory.CreateDocumentAsync(request, allowFallback: false) is not { } text)
        {
            return null;
        }

        var index = group.Tabs.IndexOf(viewer);
        area.Insert(text, group);
        await area.CloseAsync(viewer);
        area.MoveToGroup(text, group, index);
        return text;
    }
}
