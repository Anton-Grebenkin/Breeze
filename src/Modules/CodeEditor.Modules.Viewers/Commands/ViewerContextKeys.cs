using CodeEditor.Core.Context;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Viewers.Commands;

/// <summary>
/// Context keys for the <c>when</c> clauses of viewer commands: a viewer tab is active, an image or SVG (zoom), SVG
/// (open as text), the hex view (go to offset). Updated when the active tab changes.
/// </summary>
public sealed class ViewerContextKeys(EditorAreaViewModel editors, IContextKeyService context) : IDisposable
{
    public const string ViewerActiveKey = "fileViewerActive";
    public const string ZoomableActiveKey = "imageViewerActive";
    public const string TextActiveKey = "fileViewerIsText";
    public const string HexActiveKey = "hexViewerActive";

    public void Start()
    {
        editors.ActiveDocumentChanged += OnActiveChanged;
        Update();
    }

    public void Dispose() => editors.ActiveDocumentChanged -= OnActiveChanged;

    private void OnActiveChanged(object? sender, EventArgs e) => Update();

    private void Update()
    {
        var active = editors.Active?.Editor;
        context.Set(ViewerActiveKey, active is ViewerViewModel);
        context.Set(ZoomableActiveKey, active is IZoomableViewer);
        context.Set(TextActiveKey, active is ViewerViewModel { CanOpenAsText: true });
        context.Set(HexActiveKey, active is HexViewerViewModel);
    }
}
