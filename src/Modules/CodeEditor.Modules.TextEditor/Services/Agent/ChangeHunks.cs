using CodeEditor.Core.Documents;
using CodeEditor.Core.Text;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Agent change hunks from a diff of the original and current text (<see cref="LineDiff"/>), accepted or rejected one
/// at a time, as in Cursor and Copilot. Rejecting edits the buffer as one undo step; accepting yields a new original
/// that already contains the hunk. Lines are compared without <c>\r</c>; rebuilt text uses the current file's line
/// ending.
/// </summary>
public static class ChangeHunks
{
    public static IReadOnlyList<ChangeHunk> Compute(string original, string current)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(current);
        // Fully qualified: this module has its own LineDiff (an approximate counter for the feed).
        var lines = Core.Text.LineDiff.Compute(original, current);
        var hunks = new List<ChangeHunk>();
        int oldCursor = 0, newCursor = 0, index = 0;
        while (index < lines.Count)
        {
            if (lines[index].Kind == DiffKind.Unchanged)
            {
                oldCursor = lines[index].OldLine;
                newCursor = lines[index].NewLine;
                index++;
                continue;
            }

            var removed = new List<string>();
            var added = 0;
            while (index < lines.Count && lines[index].Kind != DiffKind.Unchanged)
            {
                if (lines[index].Kind == DiffKind.Removed)
                {
                    removed.Add(lines[index].Text);
                }
                else
                {
                    added++;
                }

                index++;
            }

            hunks.Add(new ChangeHunk(hunks.Count, oldCursor + 1, removed, newCursor + 1, added));
            oldCursor += removed.Count;
            newCursor += added;
        }

        return hunks;
    }

    /// <summary>The original with the hunk accepted: its removed lines replaced by the added ones.</summary>
    public static string Accept(string original, string current, ChangeHunk hunk)
    {
        ArgumentNullException.ThrowIfNull(hunk);
        var originalLines = Split(original);
        var currentLines = Split(current);
        var added = currentLines.Skip(hunk.NewStart - 1).Take(hunk.AddedCount);
        var lines = originalLines.Take(hunk.OldStart - 1).Concat(added).Concat(originalLines.Skip(hunk.OldStart - 1 + hunk.RemovedLines.Count));
        return string.Join(NewLine(current), lines);
    }

    /// <summary>Rejects the hunk in the buffer: added lines are replaced by the removed ones in a single edit.</summary>
    public static void Reject(ITextBuffer buffer, ChangeHunk hunk)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(hunk);
        var current = buffer.GetText();
        var currentLines = Split(current);
        var lines = currentLines.Take(hunk.NewStart - 1).Concat(hunk.RemovedLines).Concat(currentLines.Skip(hunk.NewStart - 1 + hunk.AddedCount));
        var rejected = string.Join(NewLine(current), lines);
        ReplaceMinimal(buffer, current, rejected);
    }

    // Common prefix and suffix are left alone: a minimal edit keeps the caret and scroll position.
    private static void ReplaceMinimal(ITextBuffer buffer, string current, string next)
    {
        var prefix = 0;
        while (prefix < current.Length && prefix < next.Length && current[prefix] == next[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < current.Length - prefix && suffix < next.Length - prefix && current[^(suffix + 1)] == next[^(suffix + 1)])
        {
            suffix++;
        }

        buffer.Replace(prefix, current.Length - prefix - suffix, next[prefix..(next.Length - suffix)]);
    }

    private static string[] Split(string text) => text.Length == 0 ? [] : [.. text.Split('\n').Select(line => line.TrimEnd('\r'))];

    private static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
