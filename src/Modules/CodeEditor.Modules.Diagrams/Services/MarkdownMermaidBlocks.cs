using System.Text;

namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>
/// Markdown <c>```mermaid</c> blocks as GitHub and GitLab see them: a CommonMark fenced block (three or more <c>`</c>
/// or <c>~</c>, indented up to three spaces) whose info string starts with the word <c>mermaid</c>. A block without a
/// closing fence runs to the end of the file, so the diagram shows while it is being typed. The opening fence indent is
/// removed from the block lines. One pass over the text, O(n). Blocks inside block quotes (<c>&gt; ```</c>) and deeply
/// nested lists are not found.
/// </summary>
public static class MarkdownMermaidBlocks
{
    private const string Language = "mermaid";
    private const int MaxFenceIndent = 3;
    private const int MinFenceLength = 3;

    public static IReadOnlyList<DiagramSource> Find(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var blocks = new List<DiagramSource>();
        Fence? open = null;
        var content = new StringBuilder();
        var lineNumber = 0;
        foreach (var range in Lines(markdown))
        {
            var line = markdown.AsSpan(range);
            lineNumber++;
            if (open is null)
            {
                open = TryOpen(line, lineNumber);
                content.Clear();
                continue;
            }

            if (IsClosing(line, open.Value))
            {
                Add(blocks, open.Value, content);
                open = null;
                continue;
            }

            if (open.Value.IsMermaid)
            {
                content.Append(Unindent(line, open.Value.Indent)).Append('\n');
            }
        }

        if (open is { } unclosed)
        {
            Add(blocks, unclosed, content);
        }

        return blocks;
    }

    private static void Add(List<DiagramSource> blocks, Fence fence, StringBuilder content)
    {
        if (fence.IsMermaid)
        {
            blocks.Add(new DiagramSource(content.ToString(), fence.Line + 1, blocks.Count + 1));
        }
    }

    // Line ranges without the line break (a "\r" before "\n" is dropped): no substring allocation per line.
    private static IEnumerable<Range> Lines(string text)
    {
        var start = 0;
        while (start <= text.Length)
        {
            var end = text.IndexOf('\n', start);
            var stop = end < 0 ? text.Length : end;
            var length = stop > start && text[stop - 1] == '\r' ? stop - start - 1 : stop - start;
            yield return start..(start + length);
            if (end < 0)
            {
                yield break;
            }

            start = end + 1;
        }
    }

    private static Fence? TryOpen(ReadOnlySpan<char> line, int lineNumber)
    {
        var indent = Indent(line);
        if (indent > MaxFenceIndent || indent >= line.Length || line[indent] is not ('`' or '~'))
        {
            return null;
        }

        var marker = line[indent];
        var length = Run(line, indent, marker);
        if (length < MinFenceLength)
        {
            return null;
        }

        var info = line[(indent + length)..].Trim();
        if (marker == '`' && info.Contains('`'))
        {
            return null;
        }

        return new Fence(marker, length, indent, lineNumber, IsMermaid(info));
    }

    private static bool IsClosing(ReadOnlySpan<char> line, Fence fence)
    {
        var indent = Indent(line);
        if (indent > MaxFenceIndent || indent >= line.Length || line[indent] != fence.Marker)
        {
            return false;
        }

        var length = Run(line, indent, fence.Marker);
        return length >= fence.Length && line[(indent + length)..].IsWhiteSpace();
    }

    private static bool IsMermaid(ReadOnlySpan<char> info)
    {
        var end = info.IndexOfAny(' ', '\t');
        var word = end < 0 ? info : info[..end];
        return word.Equals(Language, StringComparison.OrdinalIgnoreCase);
    }

    private static int Indent(ReadOnlySpan<char> line) => Run(line, 0, ' ');

    private static int Run(ReadOnlySpan<char> line, int start, char marker)
    {
        var length = line[start..].IndexOfAnyExcept(marker);
        return length < 0 ? line.Length - start : length;
    }

    private static ReadOnlySpan<char> Unindent(ReadOnlySpan<char> line, int indent) =>
        line[Math.Min(indent, Indent(line))..];

    private readonly record struct Fence(char Marker, int Length, int Indent, int Line, bool IsMermaid);
}
