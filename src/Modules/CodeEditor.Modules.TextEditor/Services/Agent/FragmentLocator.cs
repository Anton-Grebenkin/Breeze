using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.TextEditor.Resources;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Locates an agent edit fragment in stages, like Copilot, Claude Code and Codex: exact → straight instead of curly
/// quotes → line by line ignoring trailing spaces → ignoring indentation (new text is re-indented to the file's
/// style) → also ignoring dash, quote and special-space differences (<see cref="Typography"/>) → with over-escaped
/// quotes unescaped in both fragments. The last three stages attach a "check the result" warning. Ambiguity and
/// misses are errors with line numbers and a similar spot, so the model fixes the call on the first retry.
/// </summary>
/// <remarks>Each stage is O(n·m) in file and fragment lines at worst; fragments are usually a few lines.</remarks>
public static class FragmentLocator
{
    private const int HintContextLines = 3;
    private const int MaxListedOccurrences = 5;
    private const string EscapedQuote = "\\\"";

    private enum LineMatch
    {
        TrailingSpace,
        Indent,
        Typography,
    }

    public static LocatedFragment Locate(string text, string oldText, string newText, string relative)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        return Exact(text, oldText, newText, relative)
            ?? Exact(text, StraightQuotes(oldText), StraightQuotes(newText), relative)
            ?? ByLines(text, oldText, newText, relative, LineMatch.TrailingSpace)
            ?? ByLines(text, oldText, newText, relative, LineMatch.Indent)
            ?? ByLines(text, oldText, newText, relative, LineMatch.Typography)
            ?? Unescaped(text, oldText, newText, relative)
            ?? throw NotFound(text, oldText, relative);
    }

    // The model over-escaped quotes (cref=\"X\" with a literal backslash) and no exact match exists:
    // unescape both fragments and warn.
    private static LocatedFragment? Unescaped(string text, string oldText, string newText, string relative)
    {
        if (!oldText.Contains(EscapedQuote, StringComparison.Ordinal))
        {
            return null;
        }

        var (old, replacement) = (oldText.Replace(EscapedQuote, "\"", StringComparison.Ordinal), newText.Replace(EscapedQuote, "\"", StringComparison.Ordinal));
        var fragment = Exact(text, old, replacement, relative) ?? ByLines(text, old, replacement, relative, LineMatch.Indent);
        return fragment is null ? null : fragment with
        {
            Warning = Format(Strings.QuotesUnescaped, relative),
        };
    }

    private static LocatedFragment? Exact(string text, string oldText, string newText, string relative)
    {
        var offsets = Occurrences(text, oldText);
        return offsets.Count switch
        {
            0 => null,
            1 => new LocatedFragment(offsets[0], oldText.Length, newText, null),
            _ => throw Ambiguous(relative, offsets.Select(offset => LineOf(text, offset))),
        };
    }

    private static LocatedFragment? ByLines(string text, string oldText, string newText, string relative, LineMatch match)
    {
        var lines = TextLines.Split(text);
        var wanted = TextLines.Split(oldText);
        var trailingBreak = wanted.Count > 1 && wanted[^1].End == wanted[^1].Start;
        var count = trailingBreak ? wanted.Count - 1 : wanted.Count;
        var starts = Enumerable.Range(0, Math.Max(0, lines.Count - count + 1))
            .Where(start => Enumerable.Range(0, count).All(i => SameLine(text, lines[start + i], oldText, wanted[i], match)))
            .ToList();
        if (starts.Count == 0)
        {
            return null;
        }

        if (starts.Count > 1)
        {
            throw Ambiguous(relative, starts.Select(start => start + 1));
        }

        var first = lines[starts[0]];
        var last = lines[starts[0] + count - 1];
        var end = trailingBreak ? last.NextStart : last.End;
        if (match == LineMatch.TrailingSpace)
        {
            return new LocatedFragment(first.Start, end - first.Start, newText, null);
        }

        var line = starts[0] + 1;
        var found = lines.Skip(starts[0]).Take(count).ToList();
        var fileIndent = IndentOf(text, found);
        var fileUsesTabs = fileIndent.Contains('\t', StringComparison.Ordinal) || (fileIndent.Length == 0 && text.Contains("\n\t", StringComparison.Ordinal));
        var replacement = match == LineMatch.Typography ? KeepFileTypography(newText, text, found, oldText, wanted) : newText;
        var warning = match == LineMatch.Typography ? Strings.FoundIgnoringTypography : Strings.FoundIgnoringIndent;
        return new LocatedFragment(first.Start, end - first.Start, Indentation.Adapt(replacement, IndentOf(oldText, wanted), fileIndent, fileUsesTabs),
            Format(warning, relative, line, line + count - 1));
    }

    private static bool SameLine(string text, TextLine line, string fragment, TextLine wanted, LineMatch match)
    {
        var actual = text.AsSpan(line.Start, line.End - line.Start);
        var expected = fragment.AsSpan(wanted.Start, wanted.End - wanted.Start);
        return match switch
        {
            LineMatch.TrailingSpace => actual.TrimEnd().SequenceEqual(expected.TrimEnd()),
            LineMatch.Indent => actual.Trim().SequenceEqual(expected.Trim()),
            _ => Typography.PlainEquals(actual.Trim(), expected.Trim()),
        };
    }

    // Lines the model kept unchanged take the file's characters, so the edit doesn't alter dashes or quotes elsewhere.
    private static string KeepFileTypography(string newText, string text, IReadOnlyList<TextLine> found, string oldText, IReadOnlyList<TextLine> wanted)
    {
        var fileLines = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < found.Count; index++)
        {
            var model = oldText.AsSpan(wanted[index].Start, wanted[index].End - wanted[index].Start).Trim().ToString();
            fileLines.TryAdd(model, text.AsSpan(found[index].Start, found[index].End - found[index].Start).Trim().ToString());
        }

        return string.Join('\n', newText.Split('\n').Select(line =>
        {
            var content = line.AsSpan().Trim();
            if (content.IsEmpty || !fileLines.TryGetValue(content.ToString(), out var original))
            {
                return line;
            }

            var start = line.Length - line.AsSpan().TrimStart().Length;
            return string.Concat(line.AsSpan(0, start), original, line.AsSpan(start + content.Length));
        }));
    }

    private static string IndentOf(string source, IReadOnlyList<TextLine> lines)
    {
        foreach (var line in lines)
        {
            var span = source.AsSpan(line.Start, line.End - line.Start);
            var trimmed = span.TrimStart();
            if (!trimmed.IsEmpty)
            {
                return span[..(span.Length - trimmed.Length)].ToString();
            }
        }

        return string.Empty;
    }

    private static AgentToolException Ambiguous(string relative, IEnumerable<int> lines)
    {
        var listed = lines.Take(MaxListedOccurrences + 1).ToList();
        var more = listed.Count > MaxListedOccurrences ? Strings.AndMore : string.Empty;
        return new AgentToolException(Format(Strings.FragmentAmbiguous, relative, string.Join(", ", listed.Take(MaxListedOccurrences)), more));
    }

    private static AgentToolException NotFound(string text, string oldText, string relative)
    {
        var anchor = oldText.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        var lines = TextLines.Split(text);
        var match = anchor is null ? -1 : lines.ToList().FindIndex(line => text.AsSpan(line.Start, line.End - line.Start).Trim().SequenceEqual(anchor));
        if (match < 0)
        {
            return new AgentToolException(Format(Strings.FragmentNotFound, relative));
        }

        var snippet = string.Join('\n', Enumerable.Range(match, Math.Min(HintContextLines + oldText.Split('\n').Length, lines.Count - match))
            .Select(index => string.Create(CultureInfo.InvariantCulture, $"{index + 1}\t{text.AsSpan(lines[index].Start, lines[index].End - lines[index].Start)}")));
        return new AgentToolException($"{Format(Strings.FragmentSimilarAt, relative, match + 1)}\n{snippet}\n{Strings.CopyFragmentExactly}");
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static List<int> Occurrences(string text, string fragment)
    {
        var offsets = new List<int>();
        if (fragment.Length == 0)
        {
            return offsets;
        }

        for (var index = text.IndexOf(fragment, StringComparison.Ordinal); index >= 0; index = text.IndexOf(fragment, index + 1, StringComparison.Ordinal))
        {
            offsets.Add(index);
        }

        return offsets;
    }

    private static int LineOf(string text, int offset) => text.AsSpan(0, offset).Count('\n') + 1;

    private static string StraightQuotes(string text) =>
        text.Replace('“', '"').Replace('”', '"').Replace('„', '"').Replace('‘', '\'').Replace('’', '\'');
}
