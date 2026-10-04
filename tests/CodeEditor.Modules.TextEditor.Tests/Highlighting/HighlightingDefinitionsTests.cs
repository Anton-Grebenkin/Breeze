using CodeEditor.Modules.TextEditor.Languages;
using CodeEditor.Modules.TextEditor.Wpf.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Highlighting definitions (ADR 0036): all load, all colors are theme tokens, every language has one.</summary>
public sealed class HighlightingDefinitionsTests
{
    // Built-in colors that stay the text color: punctuation, Markdown formatting, embedded foreign code.
    private static readonly string[] DefaultTextColors =
    [
        "ASPSection", "Assignment", "BlockQuote", "Colon", "Constants", "CurlyBraces", "Emphasis", "Image", "LineBreak",
        "Position", "Punctuation", "Regex", "Selector", "Slash", "StrongEmphasis", "UnchangedText", "Value", "XmlPunctuation",
    ];

    // Text with constructs from many languages: every definition's regexes must run over it without errors.
    private const string SmokeText = """
        // comment # comment -- comment ; comment % comment %% comment
        /* block */ (* ml *) --[[ lua ]] <!-- xml --> =begin
        "string \" escaped" 'c' `tpl ${x}` \"\"\"triple\"\"\" r"raw" @"verbatim" $"interp {x}"
        if (a < b && c > d) { return foo(1, 2.5e3, 0xFF); } else [x] = $var + ${y} + $(z) + %w% + {{v}}
        key: value
          - item: 'quoted'
        [section]
        name = "x" # trailing
        <tag attr="v">text</tag> <Comp prop={1} />
        #include <x> #[attr] @decorator [<Attr>]
        SELECT * FROM t WHERE id = 1;
        GET https://example.com/api HTTP/1.1
        2024-01-01 12:00:00 ERROR Something failed
        """;

    public static TheoryData<string> Names => [.. HighlightingDefinitions.Names.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Names))]
    public void Definition_LoadsAndHighlights(string name)
    {
        var definition = TestHighlighting.CreateManager().GetDefinition(name);

        Assert.NotNull(definition);
        Assert.NotEmpty(TestHighlighting.Highlight(definition, SmokeText));
    }

    [Fact]
    public void EveryNamedColor_IsThemeTokenOrDefaultText()
    {
        var unmapped = HighlightingDefinitions.Names
            .SelectMany(name => HighlightingDefinitions.ReadXshd(name).Elements.OfType<XshdColor>())
            .Select(color => color.Name)
            .Where(name => SyntaxColorClassifier.ThemeKeyFor(name) is null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(DefaultTextColors.Order(StringComparer.Ordinal), unmapped.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void OwnDefinitions_UseOnlyNamedColors()
    {
        var withInlineColors = HighlightingDefinitions.OwnNames
            .Where(name => InlineColorCounter.Count(HighlightingDefinitions.ReadXshd(name)) > 0)
            .ToArray();

        Assert.True(withInlineColors.Length == 0, $"Цвет прямо в правиле, а не именем: {string.Join(", ", withInlineColors)}.");
    }

    [Fact]
    public void EveryCatalogLanguage_HasDefinition()
    {
        var names = HighlightingDefinitions.Names.ToHashSet(StringComparer.Ordinal);

        Assert.All(LanguageCatalog.All, language => Assert.Contains(language.Highlighting, names));
    }

    [Fact]
    public void ThemeTokenNames_MapToThemeTokens()
    {
        Assert.Equal(CodeEditor.UI.Themes.ThemeKeys.SyntaxControlKeyword, SyntaxColorClassifier.ThemeKeyFor("ControlKeyword"));
        Assert.Equal(CodeEditor.UI.Themes.ThemeKeys.EditorForeground, SyntaxColorClassifier.ThemeKeyFor("Text"));
        Assert.Equal(CodeEditor.UI.Themes.ThemeKeys.SyntaxKeyword, SyntaxColorClassifier.ThemeKeyFor("Keywords"));
        Assert.Equal(CodeEditor.UI.Themes.ThemeKeys.SyntaxControlKeyword, SyntaxColorClassifier.ThemeKeyFor("SelectionStatements"));
        Assert.Equal(CodeEditor.UI.Themes.ThemeKeys.SyntaxString, SyntaxColorClassifier.ThemeKeyFor("AttributeValue"));
    }
}
