namespace CodeEditor.Core.Files;

/// <summary>
/// Wraps <see cref="FileSystemWatcher"/>: buffers events and emits them as a batch at most once per
/// <see cref="FlushDelay"/>. Repeated events for one path collapse (the last one wins).
/// A Windows buffer overflow becomes a batch with the rescan flag.
/// </summary>
internal sealed class PhysicalFileWatcher : IFileWatcher
{
    public static readonly TimeSpan FlushDelay = TimeSpan.FromMilliseconds(100);

    private const int BufferSize = 64 * 1024;

    private readonly Func<string, bool> _isExcluded;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _flushTimer;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, FileChangeKind> _pending = new(StringComparer.OrdinalIgnoreCase);
    private bool _requiresRescan;
    private bool _flushScheduled;

    public PhysicalFileWatcher(string directory, Func<string, bool> isExcluded)
    {
        _isExcluded = isExcluded;
        _flushTimer = new Timer(_ => Flush());
        _watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = BufferSize,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        _watcher.Created += (_, e) => Enqueue(e.FullPath, FileChangeKind.Created);
        _watcher.Changed += (_, e) => Enqueue(e.FullPath, FileChangeKind.Changed);
        _watcher.Deleted += (_, e) => Enqueue(e.FullPath, FileChangeKind.Deleted);
        _watcher.Renamed += (_, e) =>
        {
            Enqueue(e.OldFullPath, FileChangeKind.Deleted);
            Enqueue(e.FullPath, FileChangeKind.Created);
        };
        _watcher.Error += (_, _) => RequestRescan();
        _watcher.EnableRaisingEvents = true;
    }

    public event EventHandler<FileChangesEventArgs>? Changed;

    public void Dispose()
    {
        _watcher.Dispose();
        _flushTimer.Dispose();
    }

    private void Enqueue(string path, FileChangeKind kind)
    {
        if (_isExcluded(path))
        {
            return;
        }

        lock (_lock)
        {
            // "Created" followed by "Changed" is still a creation.
            _pending[path] = kind == FileChangeKind.Changed && _pending.GetValueOrDefault(path, kind) == FileChangeKind.Created
                ? FileChangeKind.Created
                : kind;
            ScheduleFlush();
        }
    }

    private void RequestRescan()
    {
        lock (_lock)
        {
            _requiresRescan = true;
            ScheduleFlush();
        }
    }

    // Called under _lock. The timer starts once per batch, so the delay is bounded.
    private void ScheduleFlush()
    {
        if (!_flushScheduled)
        {
            _flushScheduled = true;
            _flushTimer.Change(FlushDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        FileChange[] changes;
        bool requiresRescan;
        lock (_lock)
        {
            changes = [.. _pending.Select(static pair => new FileChange(pair.Key, pair.Value))];
            requiresRescan = _requiresRescan;
            _pending.Clear();
            _requiresRescan = false;
            _flushScheduled = false;
        }

        if (changes.Length > 0 || requiresRescan)
        {
            Changed?.Invoke(this, new FileChangesEventArgs(changes, requiresRescan));
        }
    }
}
