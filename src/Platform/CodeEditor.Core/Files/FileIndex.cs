using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Files;

/// <summary>
/// Workspace file index. Scans in the background and prunes excluded folders (never enters <c>node_modules</c>).
/// Updates copy the array under a lock, so readers always see a complete snapshot without locking.
/// </summary>
public sealed partial class FileIndex : IFileIndex, IDisposable
{
    /// <summary>Guards against accidentally opening a drive root.</summary>
    public const int MaxFiles = 200_000;

    private readonly IWorkspace _workspace;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<FileIndex> _logger;
    private readonly Lock _lock = new();
    private IndexedFile[] _files = [];
    private CancellationTokenSource? _scan;

    public FileIndex(IWorkspace workspace, IFileSystem fileSystem, ILogger<FileIndex> logger)
    {
        _workspace = workspace;
        _fileSystem = fileSystem;
        _logger = logger;
        _workspace.Changed += OnWorkspaceChanged;
        _workspace.FilesChanged += OnFilesChanged;
        Rebuild();
    }

    public IReadOnlyList<IndexedFile> Files => Volatile.Read(ref _files);

    public event EventHandler? Changed;

    public Task WhenReady { get; private set; } = Task.CompletedTask;

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _workspace.FilesChanged -= OnFilesChanged;
        _scan?.Cancel();
        _scan?.Dispose();
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        lock (_lock)
        {
            _scan?.Cancel();
            _scan?.Dispose();
            _scan = null;
            Publish([]);
        }

        if (_workspace.Root is not { } root)
        {
            WhenReady = Task.CompletedTask;
            return;
        }

        var scan = new CancellationTokenSource();
        var token = scan.Token;
        _scan = scan;
        WhenReady = Task.Run(() =>
        {
            var files = Scan(root, root, token);

            // Check and publish under the lock so a stale scan cannot overwrite the new folder's result.
            lock (_lock)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Publish([.. files]);
            }

            LogIndexed(_logger, files.Count, root);
        }, token);
    }

    /// <summary>Depth-first walk with an explicit stack: no recursion, excluded folders are skipped.</summary>
    private List<IndexedFile> Scan(string root, string start, CancellationToken cancellationToken)
    {
        var files = new List<IndexedFile>();
        var folders = new Stack<string>([start]);
        while (folders.TryPop(out var folder) && files.Count < MaxFiles && !cancellationToken.IsCancellationRequested)
        {
            IEnumerable<FileSystemEntry> entries;
            try
            {
                entries = _fileSystem.EnumerateEntries(folder).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries.Where(entry => !_workspace.IsExcluded(entry.FullPath, entry.IsDirectory)))
            {
                if (entry.IsDirectory)
                {
                    folders.Push(entry.FullPath);
                }
                else
                {
                    files.Add(new IndexedFile(entry.FullPath, Path.GetRelativePath(root, entry.FullPath).Replace('\\', '/'), entry.Name));
                }
            }
        }

        return files;
    }

    private void OnFilesChanged(object? sender, FileChangesEventArgs e)
    {
        if (e.RequiresRescan)
        {
            Rebuild();
            return;
        }

        if (_workspace.Root is not { } root || e.Changes.All(static change => change.Kind == FileChangeKind.Changed))
        {
            return;
        }

        lock (_lock)
        {
            var files = new Dictionary<string, IndexedFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in _files)
            {
                files[file.FullPath] = file;
            }

            foreach (var change in e.Changes)
            {
                Apply(files, root, change);
            }

            Publish([.. files.Values]);
        }
    }

    /// <summary>A deleted folder removes everything inside it; a created folder is scanned in full.</summary>
    private void Apply(Dictionary<string, IndexedFile> files, string root, FileChange change)
    {
        if (change.Kind == FileChangeKind.Deleted)
        {
            var folderPrefix = change.Path + Path.DirectorySeparatorChar;
            foreach (var path in files.Keys.Where(path => path.Equals(change.Path, StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                files.Remove(path);
            }
        }
        else if (change.Kind == FileChangeKind.Created)
        {
            if (_fileSystem.DirectoryExists(change.Path))
            {
                foreach (var file in Scan(root, change.Path, CancellationToken.None))
                {
                    files[file.FullPath] = file;
                }
            }
            else if (_fileSystem.FileExists(change.Path) && !_workspace.IsExcluded(change.Path, isDirectory: false))
            {
                files[change.Path] = new IndexedFile(change.Path, Path.GetRelativePath(root, change.Path).Replace('\\', '/'), Path.GetFileName(change.Path));
            }
        }
    }

    private void Publish(IndexedFile[] files)
    {
        Volatile.Write(ref _files, files);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "File index: {Count} in {Root}")]
    private static partial void LogIndexed(ILogger logger, int count, string root);
}
