using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// git diff parsing into panel rows: old and new line numbers, file and hunk headers, notes (new, deleted, renamed,
/// binary file, no newline), "--- " lines inside a hunk, combined merge diffs, and git warnings to skip.
/// </summary>
public sealed class GitDiffParserTests
{
    [Fact]
    public void ModifiedFile_RowsHaveOldAndNewLineNumbers()
    {
        var rows = GitDiffParser.Parse(
        [
            "diff --git a/src/Заказ с пробелом.cs b/src/Заказ с пробелом.cs",
            "index 83db48f..bf269f4 100644",
            "--- a/src/Заказ с пробелом.cs\t",
            "+++ b/src/Заказ с пробелом.cs\t",
            "@@ -10,3 +10,4 @@ class Order",
            " context",
            "-old",
            "+new",
            "+added",
            " tail",
        ]);

        Assert.Equal(
        [
            new GitDiffRow(GitDiffRowKind.File, "src/Заказ с пробелом.cs"),
            new GitDiffRow(GitDiffRowKind.Hunk, "@@ -10,3 +10,4 @@ class Order"),
            new GitDiffRow(GitDiffRowKind.Context, "context", 10, 10),
            new GitDiffRow(GitDiffRowKind.Removed, "old", OldLine: 11),
            new GitDiffRow(GitDiffRowKind.Added, "new", NewLine: 11),
            new GitDiffRow(GitDiffRowKind.Added, "added", NewLine: 12),
            new GitDiffRow(GitDiffRowKind.Context, "tail", 12, 13),
        ], rows);
        Assert.Equal(("12", "13", string.Empty), (rows[^1].OldNumber, rows[^1].NewNumber, rows[^1].Marker));
        Assert.Equal(("11", string.Empty, "-"), (rows[3].OldNumber, rows[3].NewNumber, rows[3].Marker));
    }

    [Fact]
    public void NewAndDeletedFiles_HaveNotes_AndPathFromTheExistingSide()
    {
        var rows = GitDiffParser.Parse(
        [
            "diff --git a/new.txt b/new.txt",
            "new file mode 100644",
            "--- /dev/null",
            "+++ b/new.txt",
            "@@ -0,0 +1 @@",
            "+one",
            "\\ No newline at end of file",
            "diff --git a/gone.txt b/gone.txt",
            "deleted file mode 100644",
            "--- a/gone.txt",
            "+++ /dev/null",
            "@@ -1 +0,0 @@",
            "-two",
        ]);

        Assert.Equal(
        [
            new GitDiffRow(GitDiffRowKind.File, "new.txt"),
            new GitDiffRow(GitDiffRowKind.Note, "Новый файл"),
            new GitDiffRow(GitDiffRowKind.Hunk, "@@ -0,0 +1 @@"),
            new GitDiffRow(GitDiffRowKind.Added, "one", NewLine: 1),
            new GitDiffRow(GitDiffRowKind.Note, "Нет перевода строки в конце файла"),
            new GitDiffRow(GitDiffRowKind.File, "gone.txt"),
            new GitDiffRow(GitDiffRowKind.Note, "Файл удалён"),
            new GitDiffRow(GitDiffRowKind.Hunk, "@@ -1 +0,0 @@"),
            new GitDiffRow(GitDiffRowKind.Removed, "two", OldLine: 1),
        ], rows);
    }

    [Fact]
    public void RenameModeChangeAndBinary_BecomeNotes()
    {
        var rows = GitDiffParser.Parse(
        [
            "diff --git a/old name.cs b/new name.cs",
            "old mode 100644",
            "new mode 100755",
            "similarity index 100%",
            "rename from old name.cs",
            "rename to new name.cs",
            "diff --git a/logo.png b/logo.png",
            "index 1234567..89abcde 100644",
            "Binary files a/logo.png and b/logo.png differ",
        ]);

        Assert.Equal(
        [
            new GitDiffRow(GitDiffRowKind.File, "new name.cs"),
            new GitDiffRow(GitDiffRowKind.Note, "Режим файла: 100644 → 100755"),
            new GitDiffRow(GitDiffRowKind.Note, "Переименован: old name.cs → new name.cs"),
            new GitDiffRow(GitDiffRowKind.File, "logo.png"),
            new GitDiffRow(GitDiffRowKind.Note, "Двоичный файл: изменения не показываются"),
        ], rows);
    }

    [Fact]
    public void LinesLikeHeadersInsideHunk_AreContent_WarningsAreSkipped()
    {
        var rows = GitDiffParser.Parse(
        [
            "warning: in the working copy of 'a.sql', LF will be replaced by CRLF the next time Git touches it",
            "diff --git a/a.sql b/a.sql",
            "--- a/a.sql",
            "+++ b/a.sql",
            "@@ -1,2 +1,2 @@",
            "--- comment",
            "warning: in the working copy of 'a.sql', LF will be replaced by CRLF the next time Git touches it",
            "+++ comment",
            string.Empty,
        ]);

        Assert.Equal(
        [
            new GitDiffRow(GitDiffRowKind.File, "a.sql"),
            new GitDiffRow(GitDiffRowKind.Hunk, "@@ -1,2 +1,2 @@"),
            new GitDiffRow(GitDiffRowKind.Removed, "-- comment", OldLine: 1),
            new GitDiffRow(GitDiffRowKind.Added, "++ comment", NewLine: 1),
            new GitDiffRow(GitDiffRowKind.Context, string.Empty, 2, 2),
        ], rows);
    }

    [Fact]
    public void CombinedMergeDiff_ShowsNewVersionNumbers()
    {
        var rows = GitDiffParser.Parse(
        [
            "diff --cc conflict.cs",
            "index 1234567,89abcde..0000000",
            "--- a/conflict.cs",
            "+++ b/conflict.cs",
            "@@@ -1,2 -1,2 +1,4 @@@",
            "  same",
            "++<<<<<<< HEAD",
            "- theirs",
            " +ours",
        ]);

        Assert.Equal(
        [
            new GitDiffRow(GitDiffRowKind.File, "conflict.cs"),
            new GitDiffRow(GitDiffRowKind.Hunk, "@@@ -1,2 -1,2 +1,4 @@@"),
            new GitDiffRow(GitDiffRowKind.Context, "same", NewLine: 1),
            new GitDiffRow(GitDiffRowKind.Added, "<<<<<<< HEAD", NewLine: 2),
            new GitDiffRow(GitDiffRowKind.Removed, "theirs"),
            new GitDiffRow(GitDiffRowKind.Added, "ours", NewLine: 3),
        ], rows);
    }

    [Fact]
    public void EmptyOutput_HasNoRows() => Assert.Empty(GitDiffParser.Parse([]));
}
