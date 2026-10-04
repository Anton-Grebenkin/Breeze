using CodeEditor.Modules.Documents.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>
/// PDF view: the browser's built-in viewer shows the file itself (search, zoom, text selection). The ViewModel only
/// checks the file and gives the view its address and a load number, which makes the view reload after changes.
/// </summary>
public sealed partial class PdfViewerViewModel(string filePath, DocumentViewerContext context) : DocumentViewerViewModel(filePath, context)
{
    [ObservableProperty]
    public partial Uri? Source { get; private set; }

    /// <summary>Load number: increments on every read so the view shows the file again.</summary>
    [ObservableProperty]
    public partial int Revision { get; private set; }

    protected override object Read(CancellationToken cancellationToken) =>
        Context.FileSystem.FileExists(FilePath) ? new Uri(FilePath) : throw new FileNotFoundException(Strings.FileMissing, FilePath);

    protected override void Show(object content)
    {
        Source = (Uri)content;
        Revision++;
    }
}
