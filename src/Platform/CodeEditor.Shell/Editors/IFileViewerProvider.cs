using CodeEditor.Core.Documents;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Viewer for files that aren't opened as text (ADR 0031): PDF, Office documents, spreadsheets, images. Registered by a
/// module in DI; the highest-priority provider wins, and files no viewer takes open as text (<see cref="IEditorProvider"/>).
/// </summary>
public interface IFileViewerProvider
{
    int Priority { get; }

    bool CanOpen(string filePath);

    /// <summary>Creates the viewer ViewModel; its view is resolved by type. Closing the tab disposes it.</summary>
    object CreateViewer(string filePath);

    /// <summary>
    /// The file is text that the viewer merely renders differently (CSV as a table): editing it as text, e.g. by the
    /// agent, replaces the viewer tab with a text tab in place. Binary files (PDF, Office) never do this.
    /// </summary>
    bool IsText(string filePath) => false;

    /// <summary>
    /// The file couldn't open as text (binary or too large); the viewer may show it instead, e.g. as hex. Asked only
    /// when no viewer took the file via <see cref="CanOpen"/>.
    /// </summary>
    bool OpensInsteadOfText(string filePath, DocumentOpenFailure failure) => false;
}
