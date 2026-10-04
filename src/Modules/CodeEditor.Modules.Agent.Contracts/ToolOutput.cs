using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Resources;

namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>Keeps tool output within a limit so a big file or long list does not eat the model context.</summary>
public static class ToolOutput
{
    public const int MaxCharacters = 60_000;

    public static string Limit(string text, int maxCharacters = MaxCharacters)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= maxCharacters)
        {
            return text;
        }

        var cut = text.LastIndexOf('\n', maxCharacters - 1);
        var kept = cut > 0 ? cut : maxCharacters;
        return string.Concat(text.AsSpan(0, kept), "\n", string.Format(CultureInfo.CurrentCulture, Strings.OutputTruncated, kept, text.Length));
    }

    /// <summary>
    /// Head and tail of a long text at line boundaries, with the middle replaced by an "N lines omitted" line. Command
    /// output matters both at the start (what ran) and the end (result and errors), so the tail is longer than the
    /// head. O(n) in text length.
    /// </summary>
    public static string HeadAndTail(string text, int headCharacters, int tailCharacters)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= headCharacters + tailCharacters)
        {
            return text;
        }

        var headEnd = text.LastIndexOf('\n', headCharacters);
        headEnd = headEnd > 0 ? headEnd : headCharacters;
        var tailStart = text.IndexOf('\n', text.Length - tailCharacters);
        tailStart = tailStart >= headEnd && tailStart < text.Length - 1 ? tailStart + 1 : text.Length - tailCharacters;
        // A line feed at the head boundary ends the head's last line, not an omitted one.
        var omitted = text.AsSpan(headEnd, tailStart - headEnd).Count('\n') - (text[headEnd] == '\n' ? 1 : 0);
        var marker = "\n" + string.Format(CultureInfo.CurrentCulture, Strings.OutputOmittedLines, Math.Max(omitted, 1)) + "\n";
        return string.Concat(text.AsSpan(0, headEnd), marker, text.AsSpan(tailStart));
    }
}
