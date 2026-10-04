using CodeEditor.Core.Text;

namespace CodeEditor.Core.Tests.Text;

public sealed class FuzzyMatcherTests
{
    [Theory]
    [InlineData("", "anything")]
    [InlineData("save", "File: Save")]
    [InlineData("fs", "File: Save")]
    [InlineData("FS", "file: save")]
    [InlineData("mwv", "src/MainWindowViewModel.cs")]
    [InlineData("abc", "abc")]
    public void TryMatch_Subsequence_Matches(string pattern, string candidate)
    {
        Assert.True(FuzzyMatcher.TryMatch(pattern, candidate, out _));
    }

    [Theory]
    [InlineData("xyz", "File: Save")]
    [InlineData("evas", "File: Save")]
    [InlineData("saves", "save")]
    [InlineData("a", "")]
    public void TryMatch_NotSubsequence_DoesNotMatch(string pattern, string candidate)
    {
        Assert.False(FuzzyMatcher.TryMatch(pattern, candidate, out var score));
        Assert.Equal(0, score);
    }

    [Theory]
    [InlineData("bar", "foo/bar", "foobar")]
    [InlineData("cm", "CommandManager", "comment")]
    [InlineData("save", "Save", "File: Save All Open Editors")]
    [InlineData("fs", "File: Save", "Refresh")]
    [InlineData("tog", "Toggle Theme", "Stop Debugging")]
    [InlineData("abc", "abc", "aXbXc")]
    [InlineData("перезап", "Файл: Перезапустить CodeEditor", "Docker: Compose: пересобрать и запустить (up --build)")]
    public void TryMatch_RanksBetterCandidateHigher(string pattern, string better, string worse)
    {
        Assert.True(FuzzyMatcher.TryMatch(pattern, better, out var betterScore));
        Assert.True(FuzzyMatcher.TryMatch(pattern, worse, out var worseScore));

        Assert.True(betterScore > worseScore, $"«{better}» = {betterScore}, «{worse}» = {worseScore}");
    }

    [Fact]
    public void TryMatch_ReportsMatchedIndices()
    {
        Span<int> indices = stackalloc int[3];

        Assert.True(FuzzyMatcher.TryMatch("fsv", "File: Save", indices, out _));

        Assert.Equal([0, 6, 8], indices.ToArray());
    }

    [Fact]
    public void TryMatch_PrefersCamelCaseBoundaries()
    {
        Span<int> indices = stackalloc int[2];

        Assert.True(FuzzyMatcher.TryMatch("cm", "CommandManager", indices, out _));

        Assert.Equal([0, 7], indices.ToArray());
    }

    [Fact]
    public void TryMatch_ShortIndexBuffer_Throws()
    {
        Assert.Throws<ArgumentException>(() => FuzzyMatcher.TryMatch("abc", "abc", new int[2], out _));
    }
}
