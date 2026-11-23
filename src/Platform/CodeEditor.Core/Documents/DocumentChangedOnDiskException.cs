namespace CodeEditor.Core.Documents;

/// <summary>
/// The file changed on disk since the document read it, and nobody had noticed until the save: writing would silently
/// discard that change. The document is now flagged (<see cref="IDocument.HasExternalChanges"/>); a save of a flagged
/// document is the caller's decision to overwrite.
/// </summary>
public sealed class DocumentChangedOnDiskException(string path) : IOException($"The file changed on disk and was not overwritten: {path}")
{
    public string Path { get; } = path;
}
