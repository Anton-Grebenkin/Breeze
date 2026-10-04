using CodeEditor.Core.Files;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// When state is re-read: after a folder opens (with a delay), once per batch of file changes, and for relevant .git
/// changes only (not objects).
/// </summary>
public sealed class GitAutoRefreshTests : IDisposable
{
    private readonly GitPanelFixture _git = new();
    private readonly ManualTimeProvider _time = new();
    private readonly GitAutoRefresh _refresh;

    public GitAutoRefreshTests() =>
        _refresh = new GitAutoRefresh(_git.Repository, _git.Workspace, _git.FileSystem, new InlineUiDispatcher(), _time);

    public void Dispose()
    {
        _refresh.Dispose();
        _git.Dispose();
    }

    [Fact]
    public async Task OpenFolder_ReadsStateAfterPause()
    {
        _git.Runner.Returns(0, GitPanelFixture.Location).Returns(0, GitPanelFixture.Status());

        _time.Advance(GitAutoRefresh.Delay - TimeSpan.FromMilliseconds(1));
        var before = _git.Runner.Requests.Count;
        _time.Advance(TimeSpan.FromMilliseconds(1));
        await _git.Repository.Refreshing;

        Assert.Equal(0, before);
        Assert.Equal(2, _git.Runner.Requests.Count);
        Assert.Equal(GitRepositoryState.Ready, _git.Repository.State);
    }

    [Fact]
    public async Task FileChanges_AreBatched_IntoOneRefresh()
    {
        await OpenAsync();
        var files = _git.FileSystem.Watchers[0];

        files.Raise(new FileChange(Path.Combine(GitPanelFixture.Root, "src", "A.cs"), FileChangeKind.Changed));
        files.Raise(new FileChange(Path.Combine(GitPanelFixture.Root, "new.txt"), FileChangeKind.Created));
        _time.Advance(GitAutoRefresh.Delay);
        await _git.Repository.Refreshing;

        Assert.Single(_git.Runner.Requests);
    }

    [Fact]
    public async Task GitDirectory_IndexChangeRefreshes_ObjectsAndLocksDoNot()
    {
        await OpenAsync();
        var gitDirectory = Path.Combine(GitPanelFixture.Root, ".git");
        var watcher = _git.FileSystem.Watchers[^1];

        watcher.Raise(
            new FileChange(Path.Combine(gitDirectory, "objects", "ab", "cdef"), FileChangeKind.Created),
            new FileChange(Path.Combine(gitDirectory, "index.lock"), FileChangeKind.Created));
        _time.Advance(GitAutoRefresh.Delay);
        await _git.Repository.Refreshing;
        var ignored = _git.Runner.Requests.Count;
        watcher.Raise(new FileChange(Path.Combine(gitDirectory, "refs", "heads", "main"), FileChangeKind.Changed));
        _time.Advance(GitAutoRefresh.Delay);
        await _git.Repository.Refreshing;

        Assert.Equal(0, ignored);
        Assert.Single(_git.Runner.Requests);
        Assert.True(GitAutoRefresh.IsRelevantGitPath(gitDirectory, Path.Combine(gitDirectory, "HEAD")));
        Assert.False(GitAutoRefresh.IsRelevantGitPath(gitDirectory, Path.Combine(gitDirectory, "logs", "refs", "heads", "main")));
    }

    // The folder is already open: the first read fires on the timer, then the request log is cleared.
    private async Task OpenAsync()
    {
        _git.Runner.Returns(0, GitPanelFixture.Location).Returns(0, GitPanelFixture.Status());
        _time.Advance(GitAutoRefresh.Delay);
        await _git.Repository.Refreshing;
        Assert.Equal(GitRepositoryState.Ready, _git.Repository.State);
        _git.Runner.Requests.Clear();
    }
}
