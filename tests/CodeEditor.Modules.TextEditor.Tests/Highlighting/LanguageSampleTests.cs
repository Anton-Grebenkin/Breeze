using System.Collections.Frozen;
using CodeEditor.Modules.TextEditor.Languages;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Each language sample highlights to the expected tokens (ADR 0036): comment, string, keyword…</summary>
public sealed class LanguageSampleTests
{
    private static readonly FrozenDictionary<string, HighlightingSample> Samples =
        HighlightingSamples.All().ToFrozenDictionary(entry => entry.Id, entry => entry.Sample, StringComparer.Ordinal);

    public static TheoryData<string> Ids => [.. Samples.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Ids))]
    public void Sample_GetsExpectedTokens(string id)
    {
        var sample = Samples[id];
        var language = LanguageCatalog.ForFile(sample.FileName);
        Assert.Equal(id, language?.Id);

        var definition = TestHighlighting.CreateManager().GetDefinition(language!.Highlighting);
        var tokens = TestHighlighting.Highlight(definition, sample.Text);

        var missing = sample.Expected.Where(expected => !tokens.Contains(expected)).ToArray();
        var unexpected = sample.Unexpected
            .Where(token => tokens.Any(actual => actual.ThemeKey == token.ThemeKey && actual.Text.Contains(token.Text, StringComparison.Ordinal)))
            .ToArray();

        Assert.True(missing.Length == 0 && unexpected.Length == 0,
            $"Нет: {string.Join("; ", missing)}{Environment.NewLine}Лишние: {string.Join("; ", unexpected)}{Environment.NewLine}" +
            $"Подсветка:{Environment.NewLine}{string.Join(Environment.NewLine, tokens)}");
    }
}
