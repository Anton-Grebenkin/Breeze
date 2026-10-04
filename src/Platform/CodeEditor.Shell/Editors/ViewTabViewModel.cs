namespace CodeEditor.Shell.Editors;

/// <summary>
/// Tab without a text document (ADR 0031): a file viewer (PDF, spreadsheet; <see cref="IFileViewerProvider"/>) or a
/// module view (<see cref="IEditorViews"/>), including a tool window moved into the editor area.
/// </summary>
/// <param name="ownsEditor">Closing the tab disposes the content; a moved tool window's content stays alive.</param>
public sealed class ViewTabViewModel(string key, string title, string? toolTip, string? filePath, object editor, bool isPreview, bool ownsEditor = true)
    : EditorTab(key, editor, isPreview)
{
    public override string Title { get; } = title;

    public override string ToolTip { get; } = toolTip ?? filePath ?? title;

    public override string? FilePath { get; } = filePath;

    protected override void Dispose(bool disposing)
    {
        if (ownsEditor)
        {
            base.Dispose(disposing);
        }
    }
}
