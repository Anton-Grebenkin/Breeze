using CodeEditor.Core.Documents;

namespace CodeEditor.Shell.Editors;

/// <summary>Text document tab. Editing pins a preview tab.</summary>
public sealed class EditorTabViewModel : EditorTab
{
    public EditorTabViewModel(IDocument document, object editor, bool isPreview)
        : base(document?.FilePath ?? throw new ArgumentNullException(nameof(document)), editor, isPreview)
    {
        Document = document;
        Document.StateChanged += OnDocumentStateChanged;
    }

    public IDocument Document { get; }

    public override string Title => Document.Name;

    public override string ToolTip => Document.FilePath;

    public override string FilePath => Document.FilePath;

    public override bool IsDirty => Document.IsDirty;

    public override bool HasProblem => Document.HasExternalChanges || Document.IsDeletedOnDisk;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Document.StateChanged -= OnDocumentStateChanged;
        }

        base.Dispose(disposing);
    }

    private void OnDocumentStateChanged(object? sender, EventArgs e)
    {
        if (Document.IsDirty)
        {
            IsPreview = false;
        }

        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasProblem));
    }
}
