using System.Collections.Frozen;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using CodeEditor.UI.Themes;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using MdInline = Markdig.Syntax.Inlines.Inline;
using WpfInline = System.Windows.Documents.Inline;

namespace CodeEditor.UI.Markdown;

/// <summary>
/// Markdown inlines → WPF inlines: emphasis, code, links, line breaks, task marks. Inline code that looks like a file
/// path (<c>src/App.cs:42</c>) becomes a link, as in VS Code chat.
/// </summary>
internal static partial class MarkdownInlines
{
    /// <summary>Code font size key: set by the theme, overridable by the viewer (<see cref="MarkdownViewer.CodeFontSize"/>).</summary>
    public const string CodeFontSizeKey = "FontSize.Code";

    private static readonly FrozenSet<string> SourceExtensions = new[]
    {
        "cs", "csx", "xaml", "csproj", "props", "targets", "sln", "slnx", "json", "jsonc", "xml", "config", "md", "txt",
        "js", "ts", "tsx", "jsx", "html", "css", "scss", "py", "ps1", "sh", "cmd", "bat", "yml", "yaml", "cpp", "h", "hpp",
        "c", "java", "vb", "fs", "sql", "resx", "editorconfig", "gitignore",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <param name="insideLink">
    /// The inlines are a link's text: nothing in it becomes a link of its own. WPF throws on a hyperlink inside a
    /// hyperlink, and one such answer broke the whole feed.
    /// </param>
    public static void AddTo(InlineCollection target, ContainerInline? source, bool insideLink = false)
    {
        if (source is null)
        {
            return;
        }

        foreach (var inline in source)
        {
            if (Convert(inline, insideLink) is { } converted)
            {
                target.Add(converted);
            }
        }
    }

    /// <summary>A project file path: has a folder or a known extension, with an optional ":line".</summary>
    public static bool LooksLikeFilePath(string text)
    {
        var match = FilePathPattern().Match(text);
        return match.Success && (text.Contains('/', StringComparison.Ordinal) || text.Contains('\\', StringComparison.Ordinal)
            || SourceExtensions.Contains(match.Groups["ext"].Value));
    }

    // ContainerInline subclasses (emphasis, link) must be matched before ContainerInline itself.
    private static WpfInline? Convert(MdInline inline, bool insideLink) => inline switch
    {
        LiteralInline literal => new Run(literal.Content.ToString()),
        CodeInline code => Code(code.Content, linkPaths: !insideLink),
        EmphasisInline emphasis => Emphasis(emphasis, insideLink),
        LinkInline link when !insideLink => Link(link.Url ?? string.Empty, link),
        AutolinkInline autolink when !insideLink => Link(autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url, text: autolink.Url),
        AutolinkInline autolink => new Run(autolink.Url),
        LineBreakInline lineBreak => lineBreak.IsHard ? new LineBreak() : new Run(" "),
        HtmlEntityInline entity => new Run(entity.Transcoded.ToString()),
        HtmlInline html => new Run(html.Tag),
        TaskList task => new Run(task.Checked ? "☑ " : "☐ "),
        ContainerInline container => Container(new Span(), container, insideLink),
        _ => null,
    };

    private static WpfInline Code(string text, bool linkPaths)
    {
        var run = new Run(text);
        run.SetResourceReference(TextElement.FontFamilyProperty, "Font.Code");
        run.SetResourceReference(TextElement.FontSizeProperty, CodeFontSizeKey);
        run.SetResourceReference(TextElement.BackgroundProperty, ThemeKeys.MarkdownInlineCodeBackground);
        run.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.MarkdownInlineCodeForeground);
        if (!linkPaths || !LooksLikeFilePath(text))
        {
            return run;
        }

        var link = CreateHyperlink(text);
        link.Inlines.Add(run);
        return link;
    }

    private static Span Emphasis(EmphasisInline emphasis, bool insideLink)
    {
        var span = Container(new Span(), emphasis, insideLink);
        if (emphasis.DelimiterChar == '~')
        {
            span.TextDecorations = TextDecorations.Strikethrough;
            return span;
        }

        if (emphasis.DelimiterCount >= 2)
        {
            span.FontWeight = FontWeights.SemiBold;
        }

        if (emphasis.DelimiterCount != 2)
        {
            span.FontStyle = FontStyles.Italic;
        }

        return span;
    }

    private static Hyperlink Link(string url, ContainerInline? content = null, string? text = null)
    {
        var link = CreateHyperlink(url);
        if (content?.FirstChild is not null)
        {
            Container(link, content, insideLink: true);
        }
        else
        {
            link.Inlines.Add(new Run(text ?? url));
        }

        return link;
    }

    private static Hyperlink CreateHyperlink(string target)
    {
        var link = new Hyperlink { Tag = target, ToolTip = target, TextDecorations = null, Cursor = System.Windows.Input.Cursors.Hand };
        link.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.MarkdownLink);
        return link;
    }

    private static TSpan Container<TSpan>(TSpan span, ContainerInline source, bool insideLink)
        where TSpan : Span
    {
        AddTo(span.Inlines, source, insideLink);
        return span;
    }

    [GeneratedRegex(@"^(?:[\w.\-]+[/\\])*[\w.\-]*\.(?<ext>[A-Za-z][A-Za-z0-9]{0,11})(?::\d+(?:[-:]\d+)?)?$")]
    private static partial Regex FilePathPattern();
}
