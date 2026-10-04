using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>
/// Prepares diagram text for rendering so that Mermaid error lines point into the user's text. Before parsing, Mermaid
/// strips front matter, <c>%%{…}%%</c> directives and <c>%%</c> comment lines with their line breaks, plus leading
/// blank lines, and numbers the remaining lines. Here comments become blank lines (Mermaid would drop them anyway; the
/// diagram means the same), so the offset exists only at the start, which <see cref="PreparedMermaid.LeadingLines"/>
/// counts. A multi-line directive in the middle of a diagram is not accounted for (rare). Line breaks are <c>\n</c>, as
/// after Mermaid's own text cleanup.
/// </summary>
public static partial class MermaidSource
{
    private const string DirectiveStart = "%%{";
    private const string DirectiveEnd = "}%%";

    public static PreparedMermaid Prepare(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var blanked = CommentLine().Replace(normalized, string.Empty);
        return new PreparedMermaid(blanked, LeadingLines(blanked), normalized.Count('\n') + 1);
    }

    // Front matter, then blank lines and directives: everything Mermaid drops before the first diagram line.
    private static int LeadingLines(string text)
    {
        var frontMatter = FrontMatter().Match(text);
        var position = frontMatter.Success ? frontMatter.Length : 0;
        var lines = frontMatter.Success ? frontMatter.ValueSpan.Count('\n') : 0;
        while (position < text.Length)
        {
            var end = text.IndexOf('\n', position);
            if (end < 0)
            {
                break;
            }

            var line = text.AsSpan(position, end - position).Trim();
            if (line.StartsWith(DirectiveStart, StringComparison.Ordinal))
            {
                end = DirectiveEndLine(text, position);
                if (end < 0)
                {
                    break;
                }
            }
            else if (!line.IsEmpty)
            {
                break;
            }

            lines += text.AsSpan(position, end - position).Count('\n') + 1;
            position = end + 1;
        }

        return lines;
    }

    // End of the line where the directive closes; -1 if it is not closed or diagram text follows it on that line.
    private static int DirectiveEndLine(string text, int start)
    {
        var close = text.IndexOf(DirectiveEnd, start, StringComparison.Ordinal);
        if (close < 0)
        {
            return -1;
        }

        var after = close + DirectiveEnd.Length;
        var end = text.IndexOf('\n', after);
        return end >= 0 && text.AsSpan(after, end - after).IsWhiteSpace() ? end : -1;
    }

    // Mermaid's front matter: three dashes at the text start, content, three dashes and the line breaks after them.
    [GeneratedRegex(@"\A-{3}\s*[\n\r](.*?)[\n\r]-{3}\s*[\n\r]+", RegexOptions.Singleline)]
    private static partial Regex FrontMatter();

    // Mermaid's comment: a line starting with "%%" that is not a "%%{" directive.
    [GeneratedRegex(@"^[ \t]*%%(?!\{)[^\n]+$", RegexOptions.Multiline)]
    private static partial Regex CommentLine();
}
