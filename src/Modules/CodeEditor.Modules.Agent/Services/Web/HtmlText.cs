using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Web;

/// <summary>
/// Page HTML as Markdown-like text for the model: page title, section headings, lists, links, tables as rows and code
/// blocks as is. Takes the main content (<c>main</c> or <c>article</c>, otherwise <c>body</c>) without scripts, styles,
/// navigation, header and footer. No DOM parsing: a few regex passes, O(n) in page length each.
/// </summary>
internal static partial class HtmlText
{
    // Code blocks are temporarily replaced with a mark so whitespace collapsing keeps their indentation.
    private const char Mark = '\u0001';

    public static string ToText(string html, Uri page)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(page);
        var title = Title().Match(html) is { Success: true } found ? Clean(found.Groups["text"].Value) : null;
        var content = Hidden().Replace(Comments().Replace(html, string.Empty), string.Empty);
        content = MainContent(content);
        content = Chrome().Replace(content, string.Empty);

        var blocks = new List<string>();
        content = Pre().Replace(content, match =>
        {
            // Pages served with CRLF keep it inside <pre>: the model gets one line ending everywhere.
            blocks.Add(WebUtility.HtmlDecode(Tags().Replace(match.Groups["code"].Value, string.Empty)).ReplaceLineEndings("\n").Trim('\n'));
            return $"\n{Mark}{blocks.Count - 1}{Mark}\n";
        });
        content = Links().Replace(content, match => Link(match, page));
        content = Headings().Replace(content, match => $"\n\n{new string('#', match.Groups["level"].Value[0] - '0')} {Clean(match.Groups["text"].Value)}\n\n");
        content = InlineCode().Replace(content, match => $"`{Clean(match.Groups["text"].Value)}`");
        content = Items().Replace(content, "\n- ");
        content = Cells().Replace(content, " | ");
        content = Breaks().Replace(content, "\n");
        content = WebUtility.HtmlDecode(Tags().Replace(content, string.Empty));

        var text = new StringBuilder();
        if (title is { Length: > 0 })
        {
            text.Append("# ").Append(title).Append("\n\n");
        }

        text.Append(Collapse(content).Trim('\n'));
        return RestoreBlocks(text.ToString(), blocks).Trim();
    }

    // One pass over the text instead of a Replace per block.
    private static string RestoreBlocks(string text, List<string> blocks) =>
        blocks.Count == 0
            ? text
            : BlockMarks().Replace(text, match =>
                int.TryParse(match.Groups["index"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < blocks.Count
                    ? "```\n" + blocks[index] + "\n```"
                    : match.Value);

    private static string MainContent(string html)
    {
        foreach (var region in new[] { Main(), Article(), Body() })
        {
            if (region.Match(html) is { Success: true } match)
            {
                return match.Groups["content"].Value;
            }
        }

        return html;
    }

    // A link becomes Markdown with an absolute URL; anchors and scripts become plain text.
    private static string Link(Match match, Uri page)
    {
        var text = Clean(Tags().Replace(match.Groups["text"].Value, string.Empty));
        var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
        return text.Length > 0 && !href.StartsWith('#') && Uri.TryCreate(page, href, out var target) && target.Scheme is "http" or "https"
            ? $"[{text}]({target.AbsoluteUri})"
            : text;
    }

    private static string Clean(string html) => Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(html, " ")), " ").Trim();

    // Lines without extra spaces, at most one empty line in a row.
    private static string Collapse(string text)
    {
        var lines = text.Split('\n').Select(static line => Spaces().Replace(line, " ").Trim());
        return EmptyLines().Replace(string.Join('\n', lines), "\n\n");
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<(?:script|style|noscript|svg|template|iframe|head)\b.*?</(?:script|style|noscript|svg|template|iframe|head)\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<(?:nav|header|footer|aside|form)\b.*?</(?:nav|header|footer|aside|form)\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Chrome();

    [GeneratedRegex(@"<title\b[^>]*>(?<text>.*?)</title\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Title();

    [GeneratedRegex(@"<main\b[^>]*>(?<content>.*?)</main\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Main();

    [GeneratedRegex(@"<article\b[^>]*>(?<content>.*?)</article\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Article();

    [GeneratedRegex(@"<body\b[^>]*>(?<content>.*)</body\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Body();

    [GeneratedRegex(@"<pre\b[^>]*>(?<code>.*?)</pre\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Pre();

    [GeneratedRegex("""<a\b[^>]*?\bhref\s*=\s*["'](?<href>[^"']*)["'][^>]*>(?<text>.*?)</a\s*>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Links();

    [GeneratedRegex(@"<h(?<level>[1-6])\b[^>]*>(?<text>.*?)</h[1-6]\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Headings();

    [GeneratedRegex(@"<code\b[^>]*>(?<text>.*?)</code\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Items();

    [GeneratedRegex(@"</(?:td|th)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex Cells();

    [GeneratedRegex(@"</?(?:p|div|section|tr|table|ul|ol|br|hr|blockquote|dl|dt|dd|figure|figcaption)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Breaks();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex EmptyLines();

    [GeneratedRegex(@"\u0001(?<index>[0-9]+)\u0001")]
    private static partial Regex BlockMarks();
}
