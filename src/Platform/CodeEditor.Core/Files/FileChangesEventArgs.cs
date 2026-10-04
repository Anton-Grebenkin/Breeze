namespace CodeEditor.Core.Files;

/// <summary>
/// Batch of file changes over a short interval. <see cref="RequiresRescan"/> means the OS dropped events
/// (buffer overflow) and the subscriber must rescan the whole folder.
/// </summary>
public sealed class FileChangesEventArgs(IReadOnlyList<FileChange> changes, bool requiresRescan) : EventArgs
{
    public IReadOnlyList<FileChange> Changes { get; } = changes;

    public bool RequiresRescan { get; } = requiresRescan;
}
