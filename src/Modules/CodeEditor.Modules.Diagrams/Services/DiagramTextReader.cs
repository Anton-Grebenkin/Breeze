using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>
/// The text of a diagram file: an open document with unsaved edits (the buffer is read on the UI thread), otherwise the
/// file on disk, read in the background so a large file does not hold up the UI. The agent, export and preview thus all
/// see the same text.
/// </summary>
public sealed class DiagramTextReader(IDocumentService documents, IFileSystem fileSystem, IUiDispatcher dispatcher)
{
    /// <returns>The text; <c>null</c> if the file does not exist or is not text.</returns>
    /// <exception cref="IOException">The file could not be read (e.g. locked by another process).</exception>
    /// <exception cref="UnauthorizedAccessException">No read permission.</exception>
    public async Task<string?> ReadAsync(string path)
    {
        string? open = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (documents.TryGet(path, out var document))
            {
                open = document.Buffer.GetText();
            }
        });

        return open ?? await Task.Run(() => fileSystem.FileExists(path) ? TextFileCodec.Decode(fileSystem.ReadAllBytes(path))?.Text : null);
    }
}
