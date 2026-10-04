using System.Globalization;
using System.Text;

namespace CodeEditor.Core.Text;

/// <summary>
/// Unified diff (<c>--- a/path</c>, <c>+++ b/path</c>, <c>@@ -12,7 +12,8 @@</c>, lines prefixed with <c>' '</c>,
/// <c>-</c>, <c>+</c>), a format every model understands; used for agent self-checks and the critic. A trailing line
/// break ends the last line rather than adding an empty one; created and deleted files use the <c>0,0</c> range.
/// </summary>
public static class UnifiedDiff
{
    /// <param name="oldText">Previous text; <c>null</c> when the file was created.</param>
    /// <param name="newText">New text; <c>null</c> when the file was deleted.</param>
    public static string Format(string path, string? oldText, string? newText, int context = LineDiff.DefaultContext)
    {
        ArgumentNullException.ThrowIfNull(path);
        var oldBody = WithoutFinalBreak(oldText);
        var newBody = WithoutFinalBreak(newText);
        if (string.Equals(oldBody, newBody, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        text.Append("--- ").Append(oldText is null ? "/dev/null" : "a/" + path).Append('\n');
        text.Append("+++ ").Append(newText is null ? "/dev/null" : "b/" + path).Append('\n');
        if (oldBody.Length == 0 || newBody.Length == 0)
        {
            AppendWhole(text, oldBody, newBody);
            return text.ToString();
        }

        foreach (var hunk in LineDiff.Hunks(LineDiff.Compute(oldBody, newBody), context))
        {
            var removed = hunk.Lines.Count(line => line.Kind != DiffKind.Added);
            var added = hunk.Lines.Count(line => line.Kind != DiffKind.Removed);
            text.Append(CultureInfo.InvariantCulture, $"@@ -{hunk.OldStart},{removed} +{hunk.NewStart},{added} @@\n");
            foreach (var line in hunk.Lines)
            {
                AppendLine(text, line.Kind switch { DiffKind.Added => '+', DiffKind.Removed => '-', _ => ' ' }, line.Text);
            }
        }

        return text.ToString();
    }

    // The file was created or emptied (deleted): every line is added or removed.
    private static void AppendWhole(StringBuilder text, string oldBody, string newBody)
    {
        var (lines, mark) = oldBody.Length == 0 ? (newBody.Split('\n'), '+') : (oldBody.Split('\n'), '-');
        text.Append(mark == '+'
            ? string.Create(CultureInfo.InvariantCulture, $"@@ -0,0 +1,{lines.Length} @@\n")
            : string.Create(CultureInfo.InvariantCulture, $"@@ -1,{lines.Length} +0,0 @@\n"));
        foreach (var line in lines)
        {
            AppendLine(text, mark, line);
        }
    }

    private static void AppendLine(StringBuilder text, char mark, string line) => text.Append(mark).Append(line.TrimEnd('\r')).Append('\n');

    private static string WithoutFinalBreak(string? text)
    {
        var value = text ?? string.Empty;
        return value.EndsWith("\r\n", StringComparison.Ordinal) ? value[..^2] : value.EndsWith('\n') ? value[..^1] : value;
    }
}
