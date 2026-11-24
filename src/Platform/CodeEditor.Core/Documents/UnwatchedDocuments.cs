using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;

namespace CodeEditor.Core.Documents;

/// <summary>
/// Open documents the folder watcher does not report: outside the folder, or excluded from it (<c>.gitignore</c>,
/// <c>bin</c>). Their write time is checked every <see cref="Period"/>, so a change on disk reloads or flags them like
/// any other file; without that the tab kept stale text and the next save discarded the change. The timer runs only
/// while such a document is open.
/// </summary>
internal sealed class UnwatchedDocuments : IDisposable
{
    public static readonly TimeSpan Period = TimeSpan.FromSeconds(2);

    private readonly IWorkspace _workspace;
    private readonly IFileSystem _fileSystem;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<IReadOnlyList<Document>> _documents;
    private readonly Action<Document> _changed;
    private readonly ITimer _timer;

    /// <param name="documents">Open documents; called on the UI thread.</param>
    /// <param name="changed">A document's file changed or disappeared; called on the UI thread.</param>
    public UnwatchedDocuments(
        IWorkspace workspace, IFileSystem fileSystem, IUiDispatcher dispatcher, TimeProvider time, Func<IReadOnlyList<Document>> documents, Action<Document> changed)
    {
        _workspace = workspace;
        _fileSystem = fileSystem;
        _dispatcher = dispatcher;
        _documents = documents;
        _changed = changed;
        _timer = time.CreateTimer(_ => _ = CheckAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _workspace.Changed += OnWorkspaceChanged;
    }

    /// <summary>The set of open documents changed; on the UI thread.</summary>
    public void Refresh()
    {
        var due = _documents().Any(document => !IsWatched(document.FilePath)) ? Period : Timeout.InfiniteTimeSpan;
        _timer.Change(due, due);
    }

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _timer.Dispose();
    }

    // Another folder: a file outside the old one may be inside the new one, and the other way round.
    private void OnWorkspaceChanged(object? sender, EventArgs e) => _dispatcher.Post(Refresh);

    private async Task CheckAsync()
    {
        Document[] unwatched = [];
        await _dispatcher.InvokeAsync(() => unwatched = [.. _documents().Where(document => !IsWatched(document.FilePath))]).ConfigureAwait(false);

        // Disk access stays off the UI thread: the file may be on a slow or disconnected drive.
        foreach (var document in unwatched.Where(ChangedOnDisk))
        {
            _dispatcher.Post(() => _changed(document));
        }
    }

    private bool ChangedOnDisk(Document document)
    {
        try
        {
            return _fileSystem.FileExists(document.FilePath)
                ? document.IsDeletedOnDisk || (!document.HasExternalChanges && _fileSystem.GetLastWriteTimeUtc(document.FilePath) != document.LastWriteTimeUtc)
                : !document.IsDeletedOnDisk;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool IsWatched(string path) =>
        _workspace.Root is { } root
        && path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && !_workspace.IsExcluded(path, isDirectory: false);
}
