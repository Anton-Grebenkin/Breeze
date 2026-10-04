using CodeEditor.Core.Text;

namespace CodeEditor.Core.Tests.Text;

public sealed class LineDiffTests
{
    [Fact]
    public void Identical_AllUnchanged()
    {
        var lines = LineDiff.Compute("a\nb\n", "a\nb\n");

        Assert.All(lines, line => Assert.Equal(DiffKind.Unchanged, line.Kind));
        Assert.Empty(LineDiff.Hunks(lines));
    }

    [Fact]
    public void ChangedLine_IsRemovedThenAdded_WithLineNumbers()
    {
        var lines = LineDiff.Compute("class A\r\n{\r\n    void Run() { }\r\n}", "class A\n{\n    void Execute() { }\n}");

        Assert.Equal(
        [
            new DiffLine(DiffKind.Unchanged, "class A", 1, 1),
            new DiffLine(DiffKind.Unchanged, "{", 2, 2),
            new DiffLine(DiffKind.Removed, "    void Run() { }", 3, 0),
            new DiffLine(DiffKind.Added, "    void Execute() { }", 0, 3),
            new DiffLine(DiffKind.Unchanged, "}", 4, 4),
        ], lines);
    }

    [Fact]
    public void InsertionAndDeletion_AreMinimal()
    {
        var lines = LineDiff.Compute("a\nb\nc\nd", "a\nc\nd\ne");

        Assert.Equal([DiffKind.Unchanged, DiffKind.Removed, DiffKind.Unchanged, DiffKind.Unchanged, DiffKind.Added], lines.Select(line => line.Kind));
    }

    [Fact]
    public void EmptySides()
    {
        Assert.Equal([DiffKind.Added, DiffKind.Added], LineDiff.Compute(string.Empty, "x\ny").Select(line => line.Kind));
        Assert.Equal([DiffKind.Removed], LineDiff.Compute("x", string.Empty).Select(line => line.Kind));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void RandomTexts_DiffRebuildsBothSides(int seed)
    {
        var random = new Random(seed);
        string Make() => string.Join('\n', Enumerable.Range(0, random.Next(0, 60)).Select(_ => "line" + random.Next(0, 8)));
        var before = Make();
        var after = Make();

        var lines = LineDiff.Compute(before, after);

        Assert.Equal(before, string.Join('\n', lines.Where(line => line.Kind != DiffKind.Added).Select(line => line.Text)));
        Assert.Equal(after, string.Join('\n', lines.Where(line => line.Kind != DiffKind.Removed).Select(line => line.Text)));
        Assert.Equal(Enumerable.Range(1, lines.Count(line => line.OldLine > 0)), lines.Where(line => line.OldLine > 0).Select(line => line.OldLine));
    }

    [Fact]
    public void Hunks_KeepContext_AndMergeNearbyChanges()
    {
        var before = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"l{i}"));
        var after = before.Replace("l5\n", "L5\n", StringComparison.Ordinal).Replace("l8\n", "L8\n", StringComparison.Ordinal).Replace("l25", "L25", StringComparison.Ordinal);

        var hunks = LineDiff.Hunks(LineDiff.Compute(before, after));

        Assert.Equal(2, hunks.Count);
        Assert.Equal((2, 2), (hunks[0].OldStart, hunks[0].NewStart));
        Assert.Equal("l2", hunks[0].Lines[0].Text);
        Assert.Equal("l11", hunks[0].Lines[^1].Text);
        Assert.Equal("l28", hunks[1].Lines[^1].Text);
    }

    [Fact]
    public void HugeRewrite_FallsBackWithoutBlowingUp()
    {
        var before = string.Join('\n', Enumerable.Range(0, 5000).Select(i => $"a{i}"));
        var after = string.Join('\n', Enumerable.Range(0, 5000).Select(i => $"b{i}"));

        var lines = LineDiff.Compute(before, after);

        Assert.Equal(10_000, lines.Count);
        Assert.Equal(5000, lines.Count(line => line.Kind == DiffKind.Removed));
    }
}
