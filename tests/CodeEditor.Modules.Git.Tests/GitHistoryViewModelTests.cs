using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels.Tabs;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// History tab: commits in pages, the first selected at once with its files and the first file's diff; "Show More" loads
/// the next page; an empty repository shows "No commits yet"; the list reloads when HEAD moves.
/// </summary>
public sealed class GitHistoryViewModelTests : IDisposable
{
    private const char Field = '\u001f';

    private readonly GitPanelFixture _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task Opens_WithFirstCommitSelected_FilesAndDiff()
    {
        await _git.OpenRepositoryAsync();
        _git.Runner
            .Returns(0, Log(("2db283567e1b42094c8db604b34b7445f2614364", "Второй"), ("e34d80c34cbc08ca3bafb0f6bbc0298d9ecf31a7", "Первый")))
            .Returns(0, "M\0src/A.cs\0A\0new.txt\0")
            .Returns(0, "diff --git a/src/A.cs b/src/A.cs\n--- a/src/A.cs\n+++ b/src/A.cs\n@@ -1 +1 @@\n-a\n+b");

        using var history = new GitHistoryViewModel(_git.Repository, _git.Reader, _git.Diffs, _git.Shell);
        await history.Loaded;
        await history.DetailsLoaded;
        await history.Diff!.Loaded;

        Assert.Equal(["Второй", "Первый"], history.Commits.Select(commit => commit.Subject));
        Assert.Same(history.Commits[0], history.SelectedCommit);
        Assert.Equal(["src/A.cs", "new.txt"], history.Files.Select(file => file.Path));
        Assert.Same(history.Files[0], history.SelectedFile);
        Assert.Equal(3, history.Diff.Rows.Count);
        Assert.False(history.HasMore);
        Assert.Null(history.ListMessage);
        Assert.Null(history.DetailsMessage);
        Assert.Equal(["log", "-z", "--no-show-signature", GitLogParser.Format, "--skip=0", "-n", "200"], _git.GitArgs(0));
        Assert.Equal(["show", "--format=", "--name-status", "-z", "--diff-merges=first-parent", "2db283567e1b42094c8db604b34b7445f2614364"], _git.GitArgs(1));
        Assert.Equal(
            ["show", "--format=", "-M", "--diff-merges=first-parent", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "2db283567e1b42094c8db604b34b7445f2614364", "--", "src/A.cs"],
            _git.GitArgs(2));
    }

    [Fact]
    public async Task FullPage_OffersMore_NextPageSkipsShownCommits()
    {
        await _git.OpenRepositoryAsync();
        var page = Enumerable.Range(0, GitReader.LogPageSize).Select(index => ($"{index:x40}", $"Коммит {index}")).ToArray();
        _git.Runner.Returns(0, Log(page)).Returns(0, string.Empty);
        using var history = new GitHistoryViewModel(_git.Repository, _git.Reader, _git.Diffs, _git.Shell);
        await history.Loaded;
        await history.DetailsLoaded;
        _git.Runner.Requests.Clear();
        _git.Runner.Returns(0, Log(("ffffffffffffffffffffffffffffffffffffffff", "Самый старый")));

        await history.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal("--skip=200", _git.GitArgs(0)[4]);
        Assert.Equal(GitReader.LogPageSize + 1, history.Commits.Count);
        Assert.False(history.HasMore);
    }

    [Fact]
    public async Task RepositoryWithoutCommits_SaysSo()
    {
        _git.Runner.Returns(0, GitPanelFixture.Location).Returns(0, "# branch.oid (initial)\0# branch.head main\0");
        await _git.Repository.RefreshAsync();
        _git.Runner.Returns(128, "fatal: your current branch 'main' does not have any commits yet");

        using var history = new GitHistoryViewModel(_git.Repository, _git.Reader, _git.Diffs, _git.Shell);
        await history.Loaded;

        Assert.Equal("Коммитов пока нет.", history.ListMessage);
        Assert.Empty(history.Commits);
    }

    [Fact]
    public async Task HeadMoved_ReloadsList_CopyHashCopiesFullHash()
    {
        await _git.OpenRepositoryAsync();
        _git.Runner.Returns(0, Log(("2db283567e1b42094c8db604b34b7445f2614364", "Второй")));
        using var history = new GitHistoryViewModel(_git.Repository, _git.Reader, _git.Diffs, _git.Shell);
        await history.Loaded;
        await history.DetailsLoaded;
        history.CopyHashCommand.Execute(null);
        _git.Runner.Requests.Clear();
        _git.Runner
            .Returns(0, "# branch.oid 9999999999999999999999999999999999999999\0# branch.head main\0")
            .Returns(0, Log(("9999999999999999999999999999999999999999", "Третий")));

        await _git.Repository.RefreshAsync();
        await history.Loaded;
        await history.DetailsLoaded;

        Assert.Equal("2db283567e1b42094c8db604b34b7445f2614364", _git.Shell.Clipboard);
        Assert.Equal(["Третий"], history.Commits.Select(commit => commit.Subject));
        Assert.Equal("log", _git.GitArgs(1)[0]);
    }

    private static string Log(params (string Hash, string Subject)[] commits) =>
        string.Concat(commits.Select(commit => string.Join(Field, commit.Hash, commit.Hash[..7], "Тест", "2026-10-03T14:56:43+07:00", string.Empty, commit.Subject, string.Empty) + "\0"));
}
