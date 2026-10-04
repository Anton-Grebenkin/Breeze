namespace CodeEditor.Core.Documents;

/// <summary>
/// Open file. Only <see cref="DocumentService"/> changes its state.
/// </summary>
internal sealed class Document : IDocument
{
    public Document(string filePath, ITextBuffer buffer, TextFileFormat format, DateTime lastWriteTimeUtc)
    {
        FilePath = filePath;
        Buffer = buffer;
        Format = format;
        LastWriteTimeUtc = lastWriteTimeUtc;
        Buffer.ModifiedChanged += OnModifiedChanged;
    }

    public string FilePath { get; }

    public string Name => Path.GetFileName(FilePath);

    public ITextBuffer Buffer { get; }

    public TextFileFormat Format { get; }

    public bool IsDirty => Buffer.IsModified;

    public bool HasExternalChanges { get; private set; }

    public bool IsDeletedOnDisk { get; private set; }

    /// <summary>Write time of the buffered file version, so our own save is not an external change.</summary>
    public DateTime LastWriteTimeUtc { get; set; }

    public event EventHandler? StateChanged;

    public void SetExternalState(bool hasExternalChanges, bool isDeletedOnDisk)
    {
        if (HasExternalChanges == hasExternalChanges && IsDeletedOnDisk == isDeletedOnDisk)
        {
            return;
        }

        HasExternalChanges = hasExternalChanges;
        IsDeletedOnDisk = isDeletedOnDisk;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Detach() => Buffer.ModifiedChanged -= OnModifiedChanged;

    private void OnModifiedChanged(object? sender, EventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    public override string ToString() => FilePath;
}
