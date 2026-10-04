namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Lines an edit added and removed, for the "+3 −1" detail in the agent feed. Lines are compared as multisets: the
/// same line before and after doesn't count as changed. No exact diff is needed; O(n) in lines.
/// </summary>
public static class LineDiff
{
    public static (int Added, int Removed) Count(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in Lines(oldText))
        {
            remaining[line] = remaining.GetValueOrDefault(line) + 1;
        }

        var added = 0;
        foreach (var line in Lines(newText))
        {
            if (remaining.GetValueOrDefault(line) > 0)
            {
                remaining[line]--;
            }
            else
            {
                added++;
            }
        }

        return (added, remaining.Values.Sum());
    }

    /// <summary>Text lines without a trailing empty one; empty text has no lines.</summary>
    public static string[] Lines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? [] : text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
    }
}
