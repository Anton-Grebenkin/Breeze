using CodeEditor.Modules.Documents.Model;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>
/// Document viewers for editor tabs (ADR 0031, 0034): PDF, Word, PowerPoint, Excel and CSV don't open as text. Creating
/// a ViewModel reads nothing and loads no format libraries; the tab's first show does that.
/// </summary>
public sealed class DocumentViewerProvider(DocumentViewerContext context) : IFileViewerProvider
{
    public int Priority => 10;

    public bool CanOpen(string filePath) => DocumentKinds.Of(filePath) != DocumentKind.None;

    /// <summary>CSV is text: an agent edit opens it in the text editor instead of the grid.</summary>
    public bool IsText(string filePath) => DocumentKinds.Of(filePath) == DocumentKind.Csv;

    public object CreateViewer(string filePath) => DocumentKinds.Of(filePath) switch
    {
        DocumentKind.Pdf => new PdfViewerViewModel(filePath, context),
        DocumentKind.Word or DocumentKind.PowerPoint => new RichDocumentViewerViewModel(filePath, context),
        _ => new SpreadsheetViewerViewModel(filePath, context),
    };
}
