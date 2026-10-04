using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Testing;

namespace CodeEditor.Modules.TextEditor.Tests;

/// <summary>Agent change hunks: computed from a diff, accepted (new original) and rejected (buffer edit).</summary>
public sealed class ChangeHunksTests
{
    private const string Original = "a\nb\nc\nd\ne\n";

    [Fact]
    public void Compute_GroupsChangedLines_WithPositions()
    {
        var hunks = ChangeHunks.Compute(Original, "a\nB\nc\nx\ny\nd\n");

        Assert.Equal(3, hunks.Count);
        Assert.Equal((0, 2, "b", 2, 1), Describe(hunks[0]));
        Assert.Equal((1, 4, "", 4, 2), Describe(hunks[1]));
        // A pure removal of "e" before the file's last (empty) line.
        Assert.Equal((2, 5, "e", 7, 0), Describe(hunks[2]));
        Assert.True(hunks[2].IsRemoval);
        Assert.True(hunks[1].Contains(5));
        Assert.False(hunks[1].Contains(6));
    }

    private static (int Index, int OldStart, string Removed, int NewStart, int Added) Describe(ChangeHunk hunk) =>
        (hunk.Index, hunk.OldStart, string.Join('|', hunk.RemovedLines), hunk.NewStart, hunk.AddedCount);

    [Fact]
    public void Compute_NoChanges_IsEmpty() => Assert.Empty(ChangeHunks.Compute(Original, Original));

    [Fact]
    public void Compute_CreatedFile_IsOneAddedHunk()
    {
        var hunk = Assert.Single(ChangeHunks.Compute(string.Empty, "x\ny\n"));

        Assert.Equal((1, 3, 0), (hunk.NewStart, hunk.AddedCount, hunk.RemovedLines.Count));
    }

    [Fact]
    public void Accept_MovesOneHunkIntoTheOriginal()
    {
        var current = "a\nB\nc\nx\ny\nd\n";
        var hunks = ChangeHunks.Compute(Original, current);

        var accepted = ChangeHunks.Accept(Original, current, hunks[1]);

        Assert.Equal("a\nb\nc\nx\ny\nd\ne\n", accepted);
        // The other hunks remain; the accepted one is gone.
        Assert.Equal([2, 7], ChangeHunks.Compute(accepted, current).Select(hunk => hunk.NewStart));
    }

    [Fact]
    public void Accept_KeepsWindowsLineEndings_OfTheCurrentText()
    {
        var current = "a\r\nB\r\nc\r\nd\r\ne\r\n";
        var hunk = Assert.Single(ChangeHunks.Compute(Original, current));

        Assert.Equal(current, ChangeHunks.Accept(Original, current, hunk));
    }

    [Fact]
    public void Reject_RestoresRemovedLines_WithOneReplace()
    {
        var buffer = new TestTextBuffer("a\nB\nc\nx\ny\nd\n");
        var hunks = ChangeHunks.Compute(Original, buffer.GetText());

        ChangeHunks.Reject(buffer, hunks[0]);
        Assert.Equal("a\nb\nc\nx\ny\nd\n", buffer.GetText());

        ChangeHunks.Reject(buffer, ChangeHunks.Compute(Original, buffer.GetText())[1]);
        Assert.Equal("a\nb\nc\nx\ny\nd\ne\n", buffer.GetText());

        ChangeHunks.Reject(buffer, Assert.Single(ChangeHunks.Compute(Original, buffer.GetText())));
        Assert.Equal(Original, buffer.GetText());
        Assert.True(buffer.CanUndo);
    }
}
