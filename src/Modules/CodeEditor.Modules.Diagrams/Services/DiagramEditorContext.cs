using CodeEditor.Core.Context;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>
/// Context keys for the <c>when</c> conditions of diagram commands: the active tab is a file that may contain diagrams
/// (<c>.mmd</c>, <c>.mermaid</c>, Markdown) or a preview tab. Updated when the active tab changes.
/// </summary>
public sealed class DiagramEditorContext(EditorAreaViewModel editors, IContextKeyService context) : IDisposable
{
    public const string DiagramActiveKey = "activeEditorIsDiagram";
    public const string PreviewActiveKey = "activeEditorIsDiagramPreview";

    public void Start()
    {
        editors.ActiveDocumentChanged += OnActiveChanged;
        Update();
    }

    public void Dispose() => editors.ActiveDocumentChanged -= OnActiveChanged;

    private void OnActiveChanged(object? sender, EventArgs e) => Update();

    private void Update()
    {
        context.Set(DiagramActiveKey, editors.ActiveDocument is { } document && DiagramFiles.CanContainDiagrams(document.FilePath));
        context.Set(PreviewActiveKey, editors.Active?.Editor is DiagramPreviewViewModel);
    }
}
