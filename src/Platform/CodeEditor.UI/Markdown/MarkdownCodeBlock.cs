using System.Windows;
using System.Windows.Documents;
using CodeEditor.UI.Resources;
using CodeEditor.UI.Themes;

namespace CodeEditor.UI.Markdown;

/// <summary>
/// Code block: a header with the language and a Copy link, then the code in a monospaced font with highlighting. The
/// code stays document text, so it is selected and copied together with the reply.
/// </summary>
internal static class MarkdownCodeBlock
{
    private const double Padding = 8;
    private const double HeaderGap = 4;

    public static Section Create(string code, string? info, ICodeColorizer? colorizer)
    {
        var language = Language(info);
        var section = new Section
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(Padding, HeaderGap, Padding, Padding),
            Margin = new Thickness(0, 0, 0, Padding),
        };
        section.SetResourceReference(Block.BorderBrushProperty, ThemeKeys.Border);
        section.SetResourceReference(TextElement.BackgroundProperty, ThemeKeys.MarkdownCodeBlockBackground);

        section.Blocks.Add(Header(language, code));
        section.Blocks.Add(Body(code, language, colorizer));
        return section;
    }

    /// <summary>First word of the fence: <c>```csharp title="x"</c> → <c>csharp</c>.</summary>
    private static string Language(string? info)
    {
        var trimmed = info.AsSpan().Trim();
        var space = trimmed.IndexOfAny(' ', '\t');
        return (space < 0 ? trimmed : trimmed[..space]).ToString();
    }

    private static Paragraph Header(string language, string code)
    {
        var header = new Paragraph { Margin = new Thickness(0, 0, 0, HeaderGap) };
        header.SetResourceReference(TextElement.FontSizeProperty, "FontSize.Small");
        header.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.TextSecondary);
        if (language.Length > 0)
        {
            header.Inlines.Add(new Run(language + "   "));
        }

        var copy = new Hyperlink(new Run(Strings.CopyCode)) { Tag = new CopyCodeRequest(code), TextDecorations = null, ToolTip = Strings.CopyCodeToolTip };
        copy.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.MarkdownLink);
        header.Inlines.Add(copy);
        return header;
    }

    private static Paragraph Body(string code, string language, ICodeColorizer? colorizer)
    {
        // Prose line height doesn't suit code: use the font's own, dense like the editor.
        var body = new Paragraph { Margin = new Thickness(0), LineHeight = double.NaN };
        body.SetResourceReference(TextElement.FontFamilyProperty, "Font.Code");
        body.SetResourceReference(TextElement.FontSizeProperty, MarkdownInlines.CodeFontSizeKey);
        if (language.Length > 0 && colorizer?.Colorize(code, language) is { } colored)
        {
            body.Inlines.AddRange(colored);
            return body;
        }

        var lines = code.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                body.Inlines.Add(new LineBreak());
            }

            body.Inlines.Add(new Run(lines[i].TrimEnd('\r')));
        }

        return body;
    }
}
