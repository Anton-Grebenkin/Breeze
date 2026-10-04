namespace CodeEditor.Shell.Editors;

/// <summary>
/// View tabs in the editor area (ADR 0031): git diffs, container logs, previews. A tab with the same id isn't
/// duplicated; the open one is activated.
/// </summary>
public sealed class EditorViews(EditorAreaViewModel editors) : IEditorViews
{
    public EditorTab Open(EditorViewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (editors.Find(request.Id) is { } open)
        {
            open.IsPreview &= request.Preview;
            editors.Activate(open);
            return open;
        }

        var group = request.ToTheSide ? editors.SideGroup() : null;
        return editors.Insert(new ViewTabViewModel(request.Id, request.Title, request.ToolTip, filePath: null, request.CreateContent(), request.Preview), group);
    }

    public void Close(string id)
    {
        if (editors.Find(id) is { } tab)
        {
            _ = editors.CloseAsync(tab);
        }
    }
}
