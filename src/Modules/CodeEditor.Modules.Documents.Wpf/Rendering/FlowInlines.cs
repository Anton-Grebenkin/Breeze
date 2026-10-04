using System.Windows;
using System.Windows.Documents;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Documents.Wpf.Rendering;

/// <summary>
/// Document text runs → WPF inlines: bold, italic, underline, strikethrough, code, links, line breaks. Colors are theme
/// resources, so the document recolors with the editor.
/// </summary>
internal static class FlowInlines
{
    public static void Add(InlineCollection target, IEnumerable<TextRun> runs)
    {
        foreach (var run in runs)
        {
            target.Add(Create(run));
        }
    }

    private static Inline Create(TextRun run)
    {
        var span = new Span();
        var lines = run.Text.Split('\n');
        for (var line = 0; line < lines.Length; line++)
        {
            if (line > 0)
            {
                span.Inlines.Add(new LineBreak());
            }

            if (lines[line].Length > 0)
            {
                span.Inlines.Add(new Run(lines[line].TrimEnd('\r')));
            }
        }

        Style(span, run);
        return run.Link is { } link && Uri.TryCreate(link, UriKind.Absolute, out var url) ? Link(span, url) : span;
    }

    private static void Style(Span span, TextRun run)
    {
        if (run.Has(TextStyle.Bold))
        {
            span.FontWeight = FontWeights.SemiBold;
        }

        if (run.Has(TextStyle.Italic))
        {
            span.FontStyle = FontStyles.Italic;
        }

        var decorations = new TextDecorationCollection();
        if (run.Has(TextStyle.Underline))
        {
            decorations.Add(TextDecorations.Underline);
        }

        if (run.Has(TextStyle.Strike))
        {
            decorations.Add(TextDecorations.Strikethrough);
        }

        if (decorations.Count > 0)
        {
            span.TextDecorations = decorations;
        }

        if (run.Has(TextStyle.Code))
        {
            span.SetResourceReference(TextElement.FontFamilyProperty, "Font.Code");
            span.SetResourceReference(TextElement.BackgroundProperty, ThemeKeys.MarkdownInlineCodeBackground);
            span.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.MarkdownInlineCodeForeground);
        }
    }

    private static Hyperlink Link(Span content, Uri url)
    {
        var hyperlink = new Hyperlink(content) { NavigateUri = url, ToolTip = url.AbsoluteUri };
        hyperlink.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.MarkdownLink);
        return hyperlink;
    }
}
