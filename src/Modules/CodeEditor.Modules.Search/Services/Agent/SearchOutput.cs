using System.Globalization;
using System.Text;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.Services.Matching;

namespace CodeEditor.Modules.Search.Services.Agent;

/// <summary>
/// <c>search_text</c> output for the model in three modes: matches grouped by file (path once, numbered lines,
/// ripgrep-style context: <c>12:</c> is a match, <c>13-</c> a neighbour line), files only, or counts.
/// Paged by <c>offset</c> and <c>maxResults</c>, ending with how to get the next page. Content pages are also capped
/// by characters: a page of long lines ends early and the hint names the next <c>offset</c>.
/// </summary>
public static class SearchOutput
{
    public const string Content = "content";
    public const string Files = "files";
    public const string Count = "count";

    /// <summary>Character cap for a page of matches (~5K tokens, ADR 0012).</summary>
    public const int MaxCharacters = 20_000;

    public static string Format(IReadOnlyList<FileSearchResult> files, string mode, int offset, int maxResults, int contextLines, Func<FileSearchResult, string[]> readLines)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(readLines);
        if (files.Count == 0)
        {
            return Strings.ToolNothingFound;
        }

        return mode switch
        {
            Files => FormatFiles(files, offset, maxResults),
            Count => FormatCount(files, maxResults),
            _ => FormatContent(files, offset, maxResults, contextLines, readLines),
        };
    }

    private static string FormatFiles(IReadOnlyList<FileSearchResult> files, int offset, int maxResults)
    {
        var output = new StringBuilder();
        foreach (var file in files.Skip(offset).Take(maxResults))
        {
            output.Append(CultureInfo.InvariantCulture, $"{file.RelativePath} ({file.Matches.Count})\n");
        }

        return output + More(Strings.ToolMoreFiles, offset, maxResults, files.Count);
    }

    private static string FormatCount(IReadOnlyList<FileSearchResult> files, int maxResults)
    {
        var header = string.Format(CultureInfo.CurrentCulture, Strings.ToolCountHeader, files.Sum(file => file.Matches.Count), files.Count);
        var output = new StringBuilder(header).Append('\n');
        foreach (var file in files.OrderByDescending(file => file.Matches.Count).Take(maxResults))
        {
            output.Append(CultureInfo.InvariantCulture, $"{file.RelativePath}: {file.Matches.Count}\n");
        }

        return output.ToString().TrimEnd();
    }

    private static string FormatContent(IReadOnlyList<FileSearchResult> files, int offset, int maxResults, int contextLines, Func<FileSearchResult, string[]> readLines)
    {
        var all = files.SelectMany(file => file.Matches.Select(match => (File: file, Match: match))).ToList();
        var output = new StringBuilder();
        var shown = 0;
        foreach (var group in all.Skip(offset).Take(maxResults).GroupBy(entry => entry.File))
        {
            if (output.Length >= MaxCharacters)
            {
                break;
            }

            output.Append(group.Key.RelativePath).Append('\n');
            shown += AppendMatches(output, [.. group.Select(entry => entry.Match)], contextLines > 0 ? readLines(group.Key) : [], contextLines);
        }

        return output + More(Strings.ToolMoreMatches, offset, shown, all.Count);
    }

    /// <returns>How many matches fit under the character cap (at least one).</returns>
    private static int AppendMatches(StringBuilder output, List<SearchMatch> matches, string[] lines, int contextLines)
    {
        var printed = 0;
        var count = 0;
        foreach (var match in matches)
        {
            if (count > 0 && output.Length >= MaxCharacters)
            {
                break;
            }

            count++;
            var first = Math.Max(Math.Max(1, match.Line - contextLines), printed + 1);
            for (var line = first; line < match.Line && line <= lines.Length; line++)
            {
                output.Append(CultureInfo.InvariantCulture, $"{line}- {lines[line - 1].TrimEnd('\r')}\n");
            }

            if (match.Line > printed)
            {
                output.Append(CultureInfo.InvariantCulture, $"{match.Line}: {match.Preview.Trim()}\n");
            }

            var last = Math.Min(lines.Length, match.Line + contextLines);
            for (var line = match.Line + 1; line <= last; line++)
            {
                output.Append(CultureInfo.InvariantCulture, $"{line}- {lines[line - 1].TrimEnd('\r')}\n");
            }

            printed = Math.Max(printed, Math.Max(match.Line, last));
        }

        return count;
    }

    /// <param name="format">Next-page hint: {0}–{1} shown of {2}, continue with offset={3}.</param>
    private static string More(string format, int offset, int maxResults, int total) =>
        offset + maxResults < total
            ? string.Format(CultureInfo.CurrentCulture, format, offset + 1, offset + maxResults, total, offset + maxResults)
            : string.Empty;
}
