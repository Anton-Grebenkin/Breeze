using CodeEditor.Modules.Search.Services;
using CodeEditor.Modules.Search.Services.Matching;

namespace CodeEditor.Modules.Search.Tests;

public sealed class MatchCollectorTests
{
    [Fact]
    public void PlainText_IgnoresCaseByDefault_IncludingCyrillic()
    {
        var matches = Collect("Привет, мир\r\nМИР тесен\nмирный", new TextSearchOptions("мир"));

        Assert.Equal([(1, 9), (2, 1), (3, 1)], matches.Select(match => (match.Line, match.Column)));
        Assert.All(matches, match => Assert.Equal(3, match.Length));
    }

    [Fact]
    public void MatchCase_IsOrdinal()
    {
        var matches = Collect("Main main MAIN", new TextSearchOptions("main") { MatchCase = true });

        Assert.Equal(6, Assert.Single(matches).Column);
    }

    [Fact]
    public void WholeWord_SkipsPartsOfWords()
    {
        var matches = Collect("item items _item item2 (item)", new TextSearchOptions("item") { WholeWord = true });

        Assert.Equal([1, 25], matches.Select(match => match.Column));
    }

    [Fact]
    public void Regex_FindsAndSkipsEmptyMatches()
    {
        var matches = Collect("var a = 1;\nvar bb = 22;", new TextSearchOptions(@"\d*") { UseRegex = true });

        Assert.Equal([(1, 9, 1), (2, 10, 2)], matches.Select(match => (match.Line, match.Column, match.Length)));
    }

    [Fact]
    public void Regex_WholeWordAndAnchors()
    {
        var matches = Collect("class A\n  class B\nsubclass C", new TextSearchOptions("^class") { UseRegex = true, WholeWord = true });

        Assert.Equal(1, Assert.Single(matches).Line);
    }

    [Fact]
    public void InvalidRegex_ThrowsReadableError()
    {
        var error = Assert.Throws<ArgumentException>(() => TextMatcher.Create(new TextSearchOptions("(") { UseRegex = true }));

        Assert.StartsWith("Неверное регулярное выражение", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_IsLineWithoutLineBreak_AndLocatesMatch()
    {
        var match = Assert.Single(Collect("first\r\n    return value;\r\nlast", new TextSearchOptions("value")));

        Assert.Equal("    return value;", match.Preview);
        Assert.Equal("value", match.Preview.Substring(match.PreviewStart, match.PreviewLength));
    }

    [Fact]
    public void Preview_OfLongLine_IsCutAroundMatch()
    {
        var line = new string('x', 500) + "needle" + new string('y', 500);

        var match = Assert.Single(Collect(line, new TextSearchOptions("needle")));

        Assert.StartsWith("…", match.Preview, StringComparison.Ordinal);
        Assert.True(match.Preview.Length <= MatchCollector.MaxPreviewLength + 1);
        Assert.Equal("needle", match.Preview.Substring(match.PreviewStart, match.PreviewLength));
        Assert.Equal(501, match.Column);
    }

    [Fact]
    public void MultilineMatch_ShowsFirstLineButKeepsFullLength()
    {
        var match = Assert.Single(Collect("a\nbegin\nend\n", new TextSearchOptions(@"begin\nend") { UseRegex = true }));

        Assert.Equal(9, match.Length);
        Assert.Equal("begin", match.Preview.Substring(match.PreviewStart, match.PreviewLength));
    }

    [Fact]
    public void Collect_StopsAtLimit()
    {
        var matches = MatchCollector.Collect(string.Concat(Enumerable.Repeat("a ", 100)), TextMatcher.Create(new TextSearchOptions("a")), maxMatches: 10);

        Assert.Equal(10, matches.Count);
    }

    private static List<SearchMatch> Collect(string text, TextSearchOptions options) =>
        MatchCollector.Collect(text, TextMatcher.Create(options), maxMatches: 1_000);
}
