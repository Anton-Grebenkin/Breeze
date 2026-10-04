namespace CodeEditor.Modules.Search.Services.Matching;

/// <summary>
/// All matches in a file's text with line numbers and previews. Single pass: line numbers are advanced by counting
/// newlines between consecutive matches (vectorized <c>Count</c>), not from the start of the file.
/// </summary>
public static class MatchCollector
{
    /// <summary>Characters kept before the match in a long preview line.</summary>
    public const int PreviewLead = 30;

    /// <summary>Preview length cap, so a one-line minified file doesn't bloat memory.</summary>
    public const int MaxPreviewLength = 250;

    private const string Ellipsis = "…";

    /// <param name="maxMatches">Per-file cap; the file scan stops once reached.</param>
    public static List<SearchMatch> Collect(string text, TextMatcher matcher, int maxMatches, int previewLead = PreviewLead)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(matcher);

        var matches = new List<SearchMatch>();
        var lineNumber = 1;
        var lineStart = 0;
        var position = 0;
        while (matches.Count < maxMatches && matcher.TryFind(text, position, out var index, out var length))
        {
            var newLines = text.AsSpan(lineStart, index - lineStart).Count('\n');
            if (newLines > 0)
            {
                lineNumber += newLines;
                lineStart = text.LastIndexOf('\n', index - 1) + 1;
            }

            matches.Add(CreateMatch(text, lineNumber, lineStart, index, length, previewLead));
            position = index + length;
        }

        return matches;
    }

    private static SearchMatch CreateMatch(string text, int lineNumber, int lineStart, int index, int length, int previewLead)
    {
        var lineEnd = text.IndexOf('\n', index);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        // A multi-line match is shown up to the end of its first line.
        var visibleLength = Math.Max(0, Math.Min(length, lineEnd - index));
        var column = index - lineStart;

        var previewFrom = column > previewLead + Ellipsis.Length ? index - previewLead : lineStart;
        var prefix = previewFrom > lineStart ? Ellipsis : string.Empty;
        var previewTo = Math.Min(lineEnd, previewFrom + MaxPreviewLength);
        visibleLength = Math.Min(visibleLength, Math.Max(0, previewTo - index));

        var preview = string.Concat(prefix, text.AsSpan(previewFrom, previewTo - previewFrom));
        return new SearchMatch(lineNumber, column + 1, length, preview, prefix.Length + index - previewFrom, visibleLength);
    }
}
