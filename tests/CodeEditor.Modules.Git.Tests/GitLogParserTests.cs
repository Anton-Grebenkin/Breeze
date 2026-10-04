using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// History (<c>git log -z</c>, U+001F-separated fields, multi-line body), commit files (<c>--name-status -z</c>, a rename
/// has the old and new path) and branches (<c>for-each-ref</c>).
/// </summary>
public sealed class GitLogParserTests
{
    private const char Field = '\u001f';

    [Fact]
    public void Log_ReadsFieldsAndMultilineBody()
    {
        var output = string.Join('\0',
            Commit("2db283567e1b42094c8db604b34b7445f2614364", "2db2835", "Тест", "2026-10-03T14:56:43+07:00", "HEAD -> main, origin/main", "Второй", "Текст\n\nещё строка\n"),
            Commit("e34d80c34cbc08ca3bafb0f6bbc0298d9ecf31a7", "e34d80c", "Тест", "2026-10-03T14:56:42+07:00", string.Empty, "Первый", string.Empty)) + "\0";

        var commits = GitLogParser.ParseLog(output);

        Assert.Equal(2, commits.Count);
        Assert.Equal(
            new GitCommitInfo("2db283567e1b42094c8db604b34b7445f2614364", "2db2835", "Тест", new DateTimeOffset(2026, 10, 3, 14, 56, 43, TimeSpan.FromHours(7)),
                "HEAD -> main, origin/main", "Второй", "Текст\n\nещё строка"),
            commits[0]);
        Assert.Equal(("Первый", string.Empty, string.Empty), (commits[1].Subject, commits[1].Refs, commits[1].Body));
    }

    [Fact]
    public void Log_WarningBeforeFirstCommit_DoesNotBreakTheHash()
    {
        var output = "warning: something\n" + Commit("2db283567e1b42094c8db604b34b7445f2614364", "2db2835", "Тест", "2026-10-03T14:56:43+07:00", string.Empty, "Тема", string.Empty) + "\0";

        var commit = Assert.Single(GitLogParser.ParseLog(output));

        Assert.Equal("2db283567e1b42094c8db604b34b7445f2614364", commit.Hash);
    }

    [Fact]
    public void Files_RenameHasOldAndNewPath()
    {
        var files = GitLogParser.ParseFiles("M\0src/Заказ.cs\0R100\0old name.cs\0new name.cs\0A\0added.txt\0D\0gone.txt\0");

        Assert.Equal(
        [
            new GitCommitFile(GitFileStatus.Modified, "src/Заказ.cs"),
            new GitCommitFile(GitFileStatus.Renamed, "new name.cs", "old name.cs"),
            new GitCommitFile(GitFileStatus.Added, "added.txt"),
            new GitCommitFile(GitFileStatus.Deleted, "gone.txt"),
        ], files);
    }

    [Fact]
    public void Branches_LocalAndRemote_SkipRemoteHead()
    {
        var branches = GitBranchParser.Parse(
        [
            "*\trefs/heads/main\t2db2835\torigin/main\tВторой",
            " \trefs/heads/feature/вход\te34d80c\t\tТема\tс табуляцией",
            " \trefs/remotes/origin/HEAD\t2db2835\t\t",
            " \trefs/remotes/origin/release\t1234567\t\tВыпуск",
            "warning: ignored line",
        ]);

        Assert.Equal(
        [
            new GitBranch("main", false, "2db2835", "Второй") { IsCurrent = true, Upstream = "origin/main" },
            new GitBranch("feature/вход", false, "e34d80c", "Тема\tс табуляцией"),
            new GitBranch("origin/release", true, "1234567", "Выпуск"),
        ], branches);
        Assert.Equal("release", branches[2].LocalName);
    }

    private static string Commit(string hash, string shortHash, string author, string date, string refs, string subject, string body) =>
        string.Join(Field, hash, shortHash, author, date, refs, subject, body);
}
