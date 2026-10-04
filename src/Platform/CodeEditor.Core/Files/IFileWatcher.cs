namespace CodeEditor.Core.Files;

/// <summary>
/// Folder watcher. Events arrive in batches on a background thread.
/// </summary>
public interface IFileWatcher : IDisposable
{
    event EventHandler<FileChangesEventArgs>? Changed;
}
