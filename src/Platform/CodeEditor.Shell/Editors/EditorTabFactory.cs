using System.Globalization;
using CodeEditor.Core.Documents;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Creates a tab for a file: a viewer (<see cref="IFileViewerProvider"/>) if one handles the file, otherwise a text
/// editor (<see cref="IEditorProvider"/>). The highest-priority provider wins. Failures go to the status bar.
/// </summary>
internal sealed class EditorTabFactory(
    IDocumentService documents, IEnumerable<IEditorProvider> providers, IEnumerable<IFileViewerProvider> viewers, StatusBarViewModel statusBar)
{
    private readonly IReadOnlyList<IEditorProvider> _providers = [.. providers.OrderByDescending(provider => provider.Priority)];
    private readonly IReadOnlyList<IFileViewerProvider> _viewers = [.. viewers.OrderByDescending(viewer => viewer.Priority)];

    public IFileViewerProvider? ViewerFor(string path) => _viewers.FirstOrDefault(viewer => viewer.CanOpen(path));

    private IFileViewerProvider? FallbackFor(string path, DocumentOpenFailure failure) =>
        failure == DocumentOpenFailure.Unreadable ? null : _viewers.FirstOrDefault(viewer => viewer.OpensInsteadOfText(path, failure));

    /// <summary>The viewer's file can also be opened as text (CSV).</summary>
    public bool IsTextViewer(string path) => ViewerFor(path)?.IsText(path) == true;

    public static ViewTabViewModel CreateViewer(IFileViewerProvider viewer, string path, bool preview) =>
        new(path, Path.GetFileName(path), path, path, viewer.CreateViewer(path), preview);

    /// <param name="allowFallback">If the file can't open as text (binary, too large), use a viewer instead if any.</param>
    public async Task<EditorTab?> CreateDocumentAsync(OpenFileRequest request, bool allowFallback)
    {
        IDocument document;
        try
        {
            document = await documents.OpenAsync(request.FilePath);
        }
        catch (DocumentOpenException exception)
        {
            if (allowFallback && FallbackFor(request.FilePath, exception.Failure) is { } fallback)
            {
                return CreateViewer(fallback, Path.GetFullPath(request.FilePath), request.Preview);
            }

            statusBar.Message = exception.Message;
            return null;
        }

        var provider = _providers.FirstOrDefault(candidate => candidate.CanOpen(document.FilePath));
        if (provider is null)
        {
            statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.NoEditor, document.Name);
            documents.Close(document);
            return null;
        }

        return new EditorTabViewModel(document, provider.CreateEditor(document), request.Preview);
    }
}
