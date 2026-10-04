using CodeEditor.Core.Processes;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Repository state for the panel: repository lookup and <c>git status</c> in its root, a folder outside a repository,
/// git not installed, refresh during refresh, action errors in the panel and status bar, one action at a time, folder
/// change.
/// </summary>
public sealed class GitRepositoryStateTests : IDisposable
{
    private readonly GitPanelFixture _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task Refresh_FindsRepository_ReadsStatusInItsRoot_WithoutWritingIndex()
    {
        _git.Runner.Returns(0, GitPanelFixture.Location).Returns(0, GitPanelFixture.Status(GitPanelFixture.Modified("src/A.cs")));

        await _git.Repository.RefreshAsync();

        Assert.Equal(GitRepositoryState.Ready, _git.Repository.State);
        Assert.Equal(new GitLocation(GitPanelFixture.Root, Path.Combine(GitPanelFixture.Root, ".git")), _git.Repository.Location);
        Assert.Equal("main", _git.Repository.Status.Head.Branch);
        Assert.Equal([new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Modified, "src/A.cs")], _git.Repository.Status.Changes);
        Assert.Equal(["--no-optional-locks", "rev-parse", "--show-toplevel", "--absolute-git-dir"], _git.Runner.Requests[0].Arguments.Skip(5));
        Assert.Equal(
            ["--literal-pathspecs", "-C", GitPanelFixture.Root, "--no-optional-locks", "status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all"],
            _git.Runner.Requests[1].Arguments.Skip(5));
        Assert.Equal(GitContextKeys.Ready, _git.Context.GetValue(GitContextKeys.State));
    }

    [Fact]
    public async Task NextRefresh_ReadsOnlyStatus()
    {
        await _git.OpenRepositoryAsync();
        _git.Runner.Returns(0, GitPanelFixture.Status(GitPanelFixture.Untracked("new.txt")));

        await _git.Repository.RefreshAsync();

        Assert.Equal(["status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all"], _git.GitArgs(0));
        Assert.Single(_git.Runner.Requests);
        Assert.True(_git.Repository.Status.HasUnstaged);
    }

    [Fact]
    public async Task FolderOutsideRepository_OffersInit_WithoutError()
    {
        _git.Runner.Returns(128, "fatal: not a git repository (or any of the parent directories): .git");

        await _git.Repository.RefreshAsync();

        Assert.Equal(GitRepositoryState.NotRepository, _git.Repository.State);
        Assert.Null(_git.Repository.Error);
        Assert.Equal(GitContextKeys.NotRepository, _git.Context.GetValue(GitContextKeys.State));
    }

    [Fact]
    public async Task GitRefusesFolder_ErrorIsShownInsteadOfRepository()
    {
        _git.Runner.Returns(128, "fatal: detected dubious ownership in repository at 'C:/repo'");

        await _git.Repository.RefreshAsync();

        Assert.Equal(GitRepositoryState.NotRepository, _git.Repository.State);
        Assert.Equal("fatal: detected dubious ownership in repository at 'C:/repo'", _git.Repository.Error);
    }

    [Fact]
    public async Task GitNotInstalled_IsItsOwnState()
    {
        using var repository = new GitRepository(
            new GitReader(new GitCli(new GitRunner(new MissingProgramRunner(), _git.Workspace))), _git.Workspace, _git.Context, _git.StatusBar);

        await repository.RefreshAsync();

        Assert.Equal(GitRepositoryState.NoGit, repository.State);
    }

    [Fact]
    public async Task RefreshDuringRefresh_RunsOnceMoreAfterIt()
    {
        await _git.OpenRepositoryAsync();
        var hold = _git.Runner.Hold = new TaskCompletionSource();

        var first = _git.Repository.RefreshAsync();
        var second = _git.Repository.RefreshAsync();
        var third = _git.Repository.RefreshAsync();
        hold.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(2, _git.Runner.Requests.Count);
    }

    [Fact]
    public async Task FailedAction_ShowsGitError_InPanelAndStatusBar_ThenRefreshes()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        _git.Runner.Returns(128, "fatal: pathspec 'x' did not match any files\nhint: more").Returns(0, GitPanelFixture.Status());

        var done = await _git.Actions.StageAsync(["x"]);

        Assert.False(done);
        Assert.Equal("fatal: pathspec 'x' did not match any files\nhint: more", _git.Repository.Error);
        Assert.Equal("Ошибка git: fatal: pathspec 'x' did not match any files", _git.StatusBar.Message);
        Assert.Equal(["add", "-A", "--", "x"], _git.GitArgs(0));
        Assert.Equal(2, _git.Runner.Requests.Count);
        Assert.False(_git.Repository.IsBusy);
    }

    [Fact]
    public async Task SecondAction_WaitsForTheFirst()
    {
        await _git.OpenRepositoryAsync();
        var hold = _git.Runner.Hold = new TaskCompletionSource();

        var pull = _git.Actions.PullAsync();
        var push = await _git.Actions.PushAsync();
        var busyMessage = _git.StatusBar.Message;
        hold.SetResult();

        Assert.True(await pull);
        Assert.False(push);
        Assert.Equal("Git: дождитесь окончания «Pull»", busyMessage);
        Assert.Equal(["pull"], _git.GitArgs(0));
        Assert.Equal("Git: Pull — готово", _git.StatusBar.Message);
    }

    [Fact]
    public async Task PushWithoutUpstream_PublishesBranchToOrigin()
    {
        await _git.OpenRepositoryAsync();

        await _git.Actions.PushAsync();

        Assert.Equal(["push", "--set-upstream", "origin", "HEAD"], _git.GitArgs(0));
    }

    [Fact]
    public async Task OtherFolder_ResetsState()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));

        _git.Workspace.Close();

        Assert.Equal(GitRepositoryState.NoFolder, _git.Repository.State);
        Assert.Null(_git.Repository.Location);
        Assert.Empty(_git.Repository.Status.Changes);
    }

    private sealed class MissingProgramRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onLine, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("git is not installed.");
    }
}
