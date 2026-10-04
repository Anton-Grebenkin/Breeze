using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Diff tab: rows and the "+N −M" summary, "No changes", git errors; a repository refresh reloads the diff but keeps
/// unchanged rows (scrolling is preserved); selected rows copy in diff order.
/// </summary>
public sealed class GitDiffViewModelTests : IDisposable
{
    private static readonly GitFileChange Change = new(GitChangeGroup.Changes, GitFileStatus.Modified, "src/A.cs");

    private static readonly string Diff = string.Join('\n',
        "diff --git a/src/A.cs b/src/A.cs",
        "--- a/src/A.cs",
        "+++ b/src/A.cs",
        "@@ -1,2 +1,2 @@",
        " class A",
        "-{}",
        "+{ }");

    private readonly GitPanelFixture _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task Loads_RowsAndSummary()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        _git.Runner.Returns(0, Diff);

        using var diff = _git.Diffs.ForChange(Change);
        await diff.Loaded;

        Assert.Equal(4, diff.Rows.Count);
        Assert.Equal(GitDiffRowKind.Hunk, diff.Rows[0].Kind);
        Assert.Equal("+1 −1", diff.Summary);
        Assert.Null(diff.Message);
        Assert.Equal("A.cs (изменения)", diff.Title);
        Assert.True(diff.CanOpenFile);
    }

    [Fact]
    public async Task EmptyDiff_SaysNoChanges_GitErrorIsShown()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        _git.Runner.Returns(0, string.Empty).Returns(128, "fatal: bad revision");

        using var empty = _git.Diffs.ForChange(Change);
        await empty.Loaded;
        using var failed = _git.Diffs.ForChange(Change);
        await failed.Loaded;

        Assert.Equal("Изменений нет.", empty.Message);
        Assert.Equal("fatal: bad revision", failed.Message);
        Assert.Empty(failed.Rows);
    }

    [Fact]
    public async Task RepositoryRefresh_RereadsDiff_SameDiffKeepsRows()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        _git.Runner.Returns(0, Diff);
        using var diff = _git.Diffs.ForChange(Change);
        await diff.Loaded;
        var rows = diff.Rows;
        _git.Runner.Returns(0, GitPanelFixture.Status(GitPanelFixture.Modified("src/A.cs"))).Returns(0, Diff);

        await _git.Repository.RefreshAsync();
        await diff.Loaded;

        Assert.Equal(3, _git.Runner.Requests.Count);
        Assert.Same(rows, diff.Rows);
    }

    [Fact]
    public async Task ClosedTab_StopsFollowingRepository()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        var diff = _git.Diffs.ForChange(Change);
        await diff.Loaded;

        diff.Dispose();
        await _git.Repository.RefreshAsync();

        Assert.Equal(2, _git.Runner.Requests.Count);
    }

    [Fact]
    public async Task Copy_SelectedRows_InDiffOrder_WithoutMarkers()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        _git.Runner.Returns(0, Diff);
        using var diff = _git.Diffs.ForChange(Change);
        await diff.Loaded;

        diff.CopyCommand.Execute(new List<object> { diff.Rows[3], diff.Rows[2] });

        Assert.Equal("{}" + Environment.NewLine + "{ }", _git.Shell.Clipboard);
    }

    [Fact]
    public async Task OpenFile_OpensWorkingFile()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        using var diff = _git.Diffs.ForChange(Change);

        await diff.OpenFileCommand.ExecuteAsync(null);

        Assert.Equal([Path.Combine(GitPanelFixture.Root, "src", "A.cs")], _git.OpenedFiles);
    }
}
