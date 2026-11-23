using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// The harness's own hints appended to a tool result: "this call was already made", "twenty reads in a row", the
/// advisor's word. They go in the same tag as editor notes, so whoever condenses the turn afterwards (memory, the
/// advisor) can tell them from what the tool said. Without the tag the hints, which sit at the end of a result where
/// the digest keeps text, were remembered as the user's requirements.
/// </summary>
public static partial class HarnessNotes
{
    private const string Open = "<reminder>";
    private const string Close = "</reminder>";

    /// <summary>The note as a separate paragraph after a tool result.</summary>
    public static string After(string note) => "\n\n" + Open + note + Close;

    /// <summary>The tool result without the harness's hints.</summary>
    public static string Strip(string result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Contains(Open, StringComparison.Ordinal) ? Note().Replace(result, string.Empty).TrimEnd() : result;
    }

    [GeneratedRegex(@"\s*<reminder>.*?</reminder>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Note();
}
