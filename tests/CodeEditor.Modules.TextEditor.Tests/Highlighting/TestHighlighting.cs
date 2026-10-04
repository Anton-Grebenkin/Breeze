using System.Windows.Media;
using CodeEditor.Modules.TextEditor.Wpf.Highlighting;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>
/// Highlighting definitions with a palette that gives each theme token a unique color, so a span's color reveals the
/// token it was mapped to.
/// </summary>
internal static class TestHighlighting
{
    private static readonly Dictionary<string, Color> PaletteByToken = SyntaxColorClassifier.PaletteKeys
        .Select((key, index) => (Key: key, Color: Color.FromRgb(1, 2, (byte)(index + 1))))
        .ToDictionary(entry => entry.Key, entry => entry.Color, StringComparer.Ordinal);

    private static readonly Dictionary<Color, string> TokenByColor = PaletteByToken.ToDictionary(entry => entry.Value, entry => entry.Key);

    public static IReadOnlyDictionary<string, Color> Palette => PaletteByToken;

    public static HighlightingManager CreateManager() => HighlightingDefinitions.CreateManager(Palette);

    /// <summary>Highlighted spans line by line: span text and theme token (<c>null</c> for the default text color).</summary>
    public static List<Token> Highlight(IHighlightingDefinition definition, string text)
    {
        var document = new TextDocument(text);
        using var highlighter = new DocumentHighlighter(document, definition);
        var tokens = new List<Token>();
        for (var line = 1; line <= document.LineCount; line++)
        {
            foreach (var section in highlighter.HighlightLine(line).Sections)
            {
                tokens.Add(new Token(document.GetText(section.Offset, section.Length), TokenOf(section.Color)));
            }
        }

        return tokens;
    }

    private static string? TokenOf(HighlightingColor color) =>
        color.Foreground?.GetColor(null) is { } value ? TokenByColor.GetValueOrDefault(value) : null;
}
