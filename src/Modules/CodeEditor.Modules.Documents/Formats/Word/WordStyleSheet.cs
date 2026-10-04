using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Word styles for reading: which paragraph styles are headings, lists, code or quotes, and what a character style
/// applies. Built-in styles are recognized by name ("heading 1"), which the file stores in English, while Russian Word
/// uses other ids ("1"). <c>basedOn</c> inheritance is followed at most <see cref="MaxDepth"/> deep so a cycle in a
/// foreign file cannot hang parsing.
/// </summary>
internal sealed class WordStyleSheet
{
    private const int MaxDepth = 10;
    private const int MaxHeadingLevel = 6;
    private const string HeadingPrefix = "heading ";

    private readonly Dictionary<string, Style> _styles;

    // Every run asks for its character style, so resolved chains are cached by id.
    private readonly Dictionary<string, TextStyle> _characterStyles = new(StringComparer.Ordinal);

    private WordStyleSheet(Dictionary<string, Style> styles) => _styles = styles;

    public static WordStyleSheet Load(MainDocumentPart main)
    {
        var styles = new Dictionary<string, Style>(StringComparer.Ordinal);
        foreach (var style in main.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
        {
            if (style.StyleId?.Value is { } id)
            {
                styles.TryAdd(id, style);
            }
        }

        return new WordStyleSheet(styles);
    }

    /// <summary>Heading level 1–6 by paragraph style; 0 if it is not a heading.</summary>
    public int HeadingLevel(string? styleId)
    {
        foreach (var style in Chain(styleId))
        {
            var name = style.StyleName?.Val?.Value ?? string.Empty;
            if (name.StartsWith(HeadingPrefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(HeadingPrefix.Length), out var level) && level > 0)
            {
                return Math.Min(level, MaxHeadingLevel);
            }

            if (name.Equals("Title", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } outline and < MaxHeadingLevel)
            {
                return outline + 1;
            }
        }

        return 0;
    }

    /// <summary>Numbering from the paragraph style (a "List Bullet" style adds the bullet itself).</summary>
    public NumberingProperties? Numbering(string? styleId) =>
        Chain(styleId).Select(style => style.StyleParagraphProperties?.NumberingProperties).FirstOrDefault(numbering => numbering is not null);

    public bool IsCode(string? styleId) => Chain(styleId).Any(style => Name(style).Contains("code", StringComparison.OrdinalIgnoreCase)
        || Name(style).Equals("HTML Preformatted", StringComparison.OrdinalIgnoreCase));

    public bool IsQuote(string? styleId) => Chain(styleId).Any(style => Name(style).EndsWith("Quote", StringComparison.OrdinalIgnoreCase));

    /// <summary>Character style formatting: "Strong" is bold, "Emphasis" italic, custom styles by properties.</summary>
    public TextStyle CharacterStyle(string? styleId)
    {
        if (styleId is null)
        {
            return TextStyle.None;
        }

        if (!_characterStyles.TryGetValue(styleId, out var result))
        {
            result = ResolveCharacterStyle(styleId);
            _characterStyles[styleId] = result;
        }

        return result;
    }

    /// <summary>Style id by its name as stored in the file ("heading 1"), or <c>null</c> if there is none.</summary>
    public string? IdByName(string name) =>
        _styles.Values.FirstOrDefault(style => Name(style).Equals(name, StringComparison.OrdinalIgnoreCase))?.StyleId?.Value;

    public bool Contains(string styleId) => _styles.ContainsKey(styleId);

    private static string Name(Style style) => style.StyleName?.Val?.Value ?? string.Empty;

    private TextStyle ResolveCharacterStyle(string styleId)
    {
        var result = TextStyle.None;
        foreach (var style in Chain(styleId).Reverse())
        {
            result = WordFormatting.Apply(result, style.StyleRunProperties?.Bold, TextStyle.Bold);
            result = WordFormatting.Apply(result, style.StyleRunProperties?.Italic, TextStyle.Italic);
            if (WordFormatting.IsMonospace(style.StyleRunProperties?.RunFonts))
            {
                result |= TextStyle.Code;
            }
        }

        return result;
    }

    // The style and its basedOn ancestors, nearest first.
    private IEnumerable<Style> Chain(string? styleId)
    {
        var id = styleId;
        for (var depth = 0; depth < MaxDepth && id is not null && _styles.TryGetValue(id, out var style); depth++)
        {
            yield return style;
            id = style.BasedOn?.Val?.Value;
        }
    }
}
