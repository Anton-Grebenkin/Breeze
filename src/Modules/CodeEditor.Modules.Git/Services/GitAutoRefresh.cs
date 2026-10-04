using System.ComponentModel;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Git.Services;

/// <summary>
/// Decides when to re-read repository state: a folder opens, workspace files change, or .git changes (a commit from the
/// terminal, a branch switch by the agent). Showing the panel refreshes at once (<c>GitPanelViewModel.OnShown</c>).
/// File events arrive in batches from background threads, so refresh runs at most once per <see cref="Delay"/>, on the
/// UI thread. Objects and locks in .git (<c>objects/</c>, <c>*.lock</c>) are ignored.
/// </summary>
public sealed class GitAutoRefresh : IDisposable
{
    /// <summary>Debounce delay: a burst of edits causes one status read.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    private readonly GitRepository _repository;
    private readonly IWorkspace _workspace;
    private readonly IFileSystem _fileSystem;
    private readonly IUiDispatcher _dispatcher;
    private readonly ITimer _timer;
    private IFileWatcher? _gitWatcher;
    private int _scheduled;

    public GitAutoRefresh(
        GitRepository repository, IWorkspace workspace, IFileSystem fileSystem, IUiDispatcher dispatcher, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _repository = repository;
        _workspace = workspace;
        _fileSystem = fileSystem;
        _dispatcher = dispatcher;
        _timer = time.CreateTimer(_ => OnTimer(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        _workspace.Changed += OnWorkspaceChanged;
        _workspace.FilesChanged += OnFilesChanged;
        _repository.PropertyChanged += OnRepositoryChanged;
        if (_workspace.Root is not null)
        {
            Schedule();
        }
    }

    /// <summary>Refreshes after <see cref="Delay"/>; an already scheduled refresh is not postponed.</summary>
    public void Schedule()
    {
        if (Interlocked.Exchange(ref _scheduled, 1) == 0)
        {
            _timer.Change(Delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _workspace.FilesChanged -= OnFilesChanged;
        _repository.PropertyChanged -= OnRepositoryChanged;
        _timer.Dispose();
        WatchGitDirectory(null);
    }

    /// <summary>Whether a .git file affects state: HEAD, index, branch refs, HEAD reflog.</summary>
    public static bool IsRelevantGitPath(string gitDirectory, string path)
    {
        var relative = Path.GetRelativePath(gitDirectory, path).Replace('\\', '/');
        return !relative.EndsWith(".lock", StringComparison.Ordinal)
            && (relative is "HEAD" or "index" or "ORIG_HEAD" or "MERGE_HEAD" or "FETCH_HEAD" or "packed-refs" or "logs/HEAD"
                || relative.StartsWith("refs/", StringComparison.Ordinal));
    }

    private void OnTimer()
    {
        Interlocked.Exchange(ref _scheduled, 0);
        _dispatcher.Post(() => _ = _repository.RefreshAsync());
    }

    // The first read is delayed too, so opening a folder at startup does not wait for git.
    private void OnWorkspaceChanged(object? sender, EventArgs e) => Schedule();

    private void OnFilesChanged(object? sender, FileChangesEventArgs e) => ScheduleFor(e);

    private void OnGitDirectoryChanged(object? sender, FileChangesEventArgs e) => ScheduleFor(e);

    // An empty batch means everything was filtered out (.git objects, excluded folders).
    private void ScheduleFor(FileChangesEventArgs e)
    {
        if (e.Changes.Count > 0 || e.RequiresRescan)
        {
            Schedule();
        }
    }

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitRepository.Location))
        {
            WatchGitDirectory(_repository.Location?.GitDirectory);
        }
    }

    private void WatchGitDirectory(string? directory)
    {
        if (_gitWatcher is not null)
        {
            _gitWatcher.Changed -= OnGitDirectoryChanged;
            _gitWatcher.Dispose();
            _gitWatcher = null;
        }

        if (directory is null || !_fileSystem.DirectoryExists(directory))
        {
            return;
        }

        try
        {
            _gitWatcher = _fileSystem.Watch(directory, path => !IsRelevantGitPath(directory, path));
            _gitWatcher.Changed += OnGitDirectoryChanged;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Without a .git watcher, state still refreshes on panel show and after actions.
        }
    }
}
