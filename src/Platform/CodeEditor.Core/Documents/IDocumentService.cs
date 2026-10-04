namespace CodeEditor.Core.Documents;

/// <summary>
/// Open documents: open, save, close and react to file changes on disk.
/// Call from the UI thread; file reads and writes run in the background.
/// </summary>
public interface IDocumentService
{
    IReadOnlyList<IDocument> Documents { get; }

    event EventHandler<IDocument>? Opened;

    event EventHandler<IDocument>? Closed;

    event EventHandler<IDocument>? Saved;

    bool TryGet(string filePath, out IDocument document);

    /// <summary>Opens the file or returns the already open document.</summary>
    /// <exception cref="DocumentOpenException">The file is binary, too large or unreadable.</exception>
    Task<IDocument> OpenAsync(string filePath, CancellationToken cancellationToken = default);

    /// <exception cref="IOException">The file could not be written.</exception>
    Task SaveAsync(IDocument document, CancellationToken cancellationToken = default);

    Task SaveAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Re-reads the file from disk, discarding unsaved edits.</summary>
    Task ReloadAsync(IDocument document, CancellationToken cancellationToken = default);

    void Close(IDocument document);
}
