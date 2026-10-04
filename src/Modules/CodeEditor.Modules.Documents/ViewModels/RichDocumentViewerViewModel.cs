using CodeEditor.Core.Text;
using CodeEditor.Modules.Documents.Formats.Slides;
using CodeEditor.Modules.Documents.Formats.Word;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>
/// Word or presentation view over the document blocks (<see cref="RichDocument"/>): headings, paragraphs, lists,
/// tables, slides. The view builds read-only formatted text from them.
/// </summary>
public sealed partial class RichDocumentViewerViewModel(string filePath, DocumentViewerContext context) : DocumentViewerViewModel(filePath, context)
{
    [ObservableProperty]
    public partial RichDocument? Document { get; private set; }

    protected override object Read(CancellationToken cancellationToken)
    {
        var bytes = ReadBytes();
        cancellationToken.ThrowIfCancellationRequested();
        return Kind == DocumentKind.PowerPoint ? PptxReader.Read(bytes) : DocxReader.Read(bytes);
    }

    protected override void Show(object content)
    {
        var document = (RichDocument)content;
        Document = document;
        Summary = Kind == DocumentKind.PowerPoint ? Plural.Format(document.SlideCount, Strings.SlideForms) : string.Empty;
    }
}
