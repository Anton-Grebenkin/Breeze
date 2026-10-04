using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Panel git arguments: paths follow "--", branch names and hashes never become options, diffs use explicit prefixes and
/// no external tool, long path lists are batched, a new branch name is validated before git runs.
/// </summary>
public sealed class GitArgumentsTests
{
    [Theory]
    [InlineData("feature/вход", true)]
    [InlineData("fix-123", true)]
    [InlineData("v1.2", true)]
    [InlineData("", false)]
    [InlineData("-f", false)]
    [InlineData(".hidden", false)]
    [InlineData("/root", false)]
    [InlineData("with space", false)]
    [InlineData("a..b", false)]
    [InlineData("a@{1}", false)]
    [InlineData("a//b", false)]
    [InlineData("a/.b", false)]
    [InlineData("ends/", false)]
    [InlineData("ends.", false)]
    [InlineData("name.lock", false)]
    [InlineData("star*", false)]
    [InlineData("colon:", false)]
    [InlineData("@", false)]
    public void BranchName_IsValidatedLikeGit(string name, bool valid) => Assert.Equal(valid, GitArguments.IsValidBranchName(name));

    [Fact]
    public void BranchAndHash_StartingWithDash_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => GitArguments.Switch("-f"));
        Assert.Throws<ArgumentException>(() => GitArguments.CreateBranch("--orphan"));
        Assert.Throws<ArgumentException>(() => GitArguments.CommitFiles("-p"));
    }

    [Fact]
    public void Diff_DependsOnGroup_PathsAfterSeparator()
    {
        Assert.Equal(["diff", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "--", "src/a b.cs"],
            GitArguments.Diff(new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Modified, "src/a b.cs")));
        Assert.Equal(["diff", "--cached", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "--", "a.cs"],
            GitArguments.Diff(new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Added, "a.cs")));
        Assert.Equal(["diff", "--cached", "-M", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "--", "old.cs", "new.cs"],
            GitArguments.Diff(new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Renamed, "new.cs", "old.cs")));
        Assert.Equal(["diff", "--no-index", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "--", "/dev/null", "new.txt"],
            GitArguments.Diff(new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Untracked, "new.txt")));
    }

    [Fact]
    public void IndexCommands_PutPathsAfterSeparator()
    {
        Assert.Equal(["add", "-A", "--", "-dash.txt"], GitArguments.Stage(["-dash.txt"]));
        Assert.Equal(["restore", "--staged", "--", "a.cs"], GitArguments.Unstage(["a.cs"], initial: false));
        Assert.Equal(["rm", "--cached", "-r", "-q", "--", "a.cs"], GitArguments.Unstage(["a.cs"], initial: true));
        Assert.Equal(["restore", "--worktree", "--", "a.cs"], GitArguments.Discard(["a.cs"]));
        Assert.Equal(["commit", "-m", "-начинается с дефиса"], GitArguments.Commit("-начинается с дефиса"));
    }

    [Fact]
    public void Status_IsPorcelainV2_WithEveryUntrackedFile() =>
        Assert.Equal(["status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all"], GitArguments.Status);

    [Fact]
    public void Batches_SplitLongPathLists()
    {
        var path = new string('a', 1000);
        var paths = Enumerable.Repeat(path, 60).ToList();

        var batches = GitArguments.Batches(paths).ToList();

        Assert.Equal(3, batches.Count);
        Assert.Equal(60, batches.Sum(batch => batch.Count));
        Assert.All(batches, batch => Assert.True(batch.Sum(item => item.Length) <= GitArguments.MaxPathCharacters));
        Assert.Empty(GitArguments.Batches([]));
    }
}
