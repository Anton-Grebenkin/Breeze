using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Parsing <c>git status --porcelain=v2 -z --branch</c>: branch headers, files by panel group, a rename with the original
/// path in the next record, conflicts, untracked files; paths with spaces and Cyrillic arrive as is.
/// </summary>
public sealed class GitStatusParserTests
{
    private const string Hashes = "N... 100644 100644 100644 83db48f84ec878fbfb30b46d16630e944e34f205 83db48f84ec878fbfb30b46d16630e944e34f205";

    [Fact]
    public void BranchHeaders_GiveBranchCommitUpstreamAndDivergence()
    {
        var status = GitStatusParser.Parse(Records(
            "# branch.oid 2db283567e1b42094c8db604b34b7445f2614364",
            "# branch.head feature/вход",
            "# branch.upstream origin/feature/вход",
            "# branch.ab +2 -3"));

        Assert.Equal(new GitHead("feature/вход", "2db283567e1b42094c8db604b34b7445f2614364") { Upstream = "origin/feature/вход", Ahead = 2, Behind = 3 }, status.Head);
        Assert.Empty(status.Changes);
    }

    [Fact]
    public void InitialAndDetachedHeads_AreRecognized()
    {
        var initial = GitStatusParser.Parse(Records("# branch.oid (initial)", "# branch.head main")).Head;
        var detached = GitStatusParser.Parse(Records("# branch.oid 2db283567e1b42094c8db604b34b7445f2614364", "# branch.head (detached)")).Head;

        Assert.True(initial.IsInitial);
        Assert.Equal("main", initial.Branch);
        Assert.True(detached.IsDetached);
        Assert.Null(detached.Branch);
        Assert.Equal("2db2835", detached.ShortCommit);
    }

    [Fact]
    public void OrdinaryEntries_GoToTheirGroups_FileChangedTwiceIsInBoth()
    {
        var status = GitStatusParser.Parse(Records(
            $"1 .M {Hashes} src/Заказ с пробелом.cs",
            $"1 M. {Hashes} staged.cs",
            $"1 MM {Hashes} both.cs",
            $"1 A. {Hashes} added.cs",
            $"1 .D {Hashes} gone.cs"));

        Assert.Equal(
        [
            new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Modified, "src/Заказ с пробелом.cs"),
            new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Modified, "staged.cs"),
            new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Modified, "both.cs"),
            new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Modified, "both.cs"),
            new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Added, "added.cs"),
            new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Deleted, "gone.cs"),
        ], status.Changes);
        Assert.True(status.Changes[^1].IsDeleted);
    }

    [Fact]
    public void Rename_TakesOriginalPathFromNextRecord_AndParsingContinues()
    {
        var status = GitStatusParser.Parse(Records(
            $"2 R. {Hashes} R100 new name.cs",
            "old name.cs",
            "? новый.txt"));

        Assert.Equal(
        [
            new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Renamed, "new name.cs", "old name.cs"),
            new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Untracked, "новый.txt"),
        ], status.Changes);
    }

    [Fact]
    public void UnmergedEntry_IsAConflictInMergeGroup()
    {
        var status = GitStatusParser.Parse(Records(
            "u UU N... 100644 100644 100644 100644 83db48f84ec878fbfb30b46d16630e944e34f205 83db48f84ec878fbfb30b46d16630e944e34f205 83db48f84ec878fbfb30b46d16630e944e34f205 conflict.cs"));

        var change = Assert.Single(status.Changes);
        Assert.Equal(new GitFileChange(GitChangeGroup.Merge, GitFileStatus.Conflict, "conflict.cs"), change);
        Assert.True(status.HasConflicts);
        Assert.False(status.HasStaged);
    }

    [Fact]
    public void EmptyOrBrokenRecords_AreSkipped()
    {
        var status = GitStatusParser.Parse(Records("1 .M", "?", "! ignored.log", "# branch.ab x y"));

        Assert.Empty(status.Changes);
        Assert.Equal(GitHead.Empty, status.Head);
    }

    private static string Records(params string[] records) => string.Join('\0', records) + "\0";
}
