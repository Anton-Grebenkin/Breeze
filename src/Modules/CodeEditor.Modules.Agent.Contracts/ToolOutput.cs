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
        var kept = cut >= maxCharacters / WastedShare ? cut : maxCharacters;
        return string.Concat(text.AsSpan(0, kept), "\n", string.Format(CultureInfo.CurrentCulture, Strings.OutputTruncated, kept, text.Length));
    }

    /// <summary>
    /// Head and tail of a long text, with the middle replaced by an "N lines omitted" line. Command output matters
    /// both at the start (what ran) and the end (result and errors), so the tail is longer than the head. The cuts are
    /// at line boundaries unless that wastes over half of a part: a huge line (a JSON answer in one line) is cut by
    /// characters, or it would vanish whole. O(n) in text length.
    /// </summary>
    public static string HeadAndTail(string text, int headCharacters, int tailCharacters)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= headCharacters + tailCharacters)
        {
            return text;
        }

        var headLine = text.LastIndexOf('\n', headCharacters);
        var headEnd = headLine >= headCharacters / WastedShare ? headLine : headCharacters;
        var tailLine = text.IndexOf('\n', text.Length - tailCharacters) + 1;
        var tailStart = tailLine > 0 && text.Length - tailLine >= tailCharacters / WastedShare ? tailLine : text.Length - tailCharacters;
        // By lines, the omitted part starts with the line feed that ends the head's last line.
        var marker = headEnd == headLine && tailStart == tailLine
            ? string.Format(CultureInfo.CurrentCulture, Strings.OutputOmittedLines, Math.Max(text.AsSpan(headEnd, tailStart - headEnd).Count('\n') - 1, 1))
            : string.Format(CultureInfo.CurrentCulture, Strings.OutputOmittedCharacters, tailStart - headEnd);
        return string.Concat(text.AsSpan(0, headEnd), "\n" + marker + "\n", text.AsSpan(tailStart));
    }

    /// <summary>A line boundary is used only if it keeps at least this share (1/N) of the allowed length.</summary>
    private const int WastedShare = 2;
}
