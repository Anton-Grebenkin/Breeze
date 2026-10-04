using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Terminal.Services;

/// <summary>
/// Parses a string built from a resource format ("Build {0} succeeded in {1} s.") back into placeholder values,
/// so the agent feed reads tool summaries in any UI language without duplicating their text. The format is matched
/// at the start of the string with lazy placeholders; the regex is built once per format.
/// </summary>
internal static partial class ResourcePattern
{
    private static readonly ConcurrentDictionary<string, Regex> Patterns = new(StringComparer.Ordinal);

    /// <returns>Values of <c>{0}</c>, <c>{1}</c>…; <c>null</c> if the text was not built from this format.</returns>
    public static IReadOnlyList<string>? Match(string format, string text)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(text);
        var match = Patterns.GetOrAdd(format, Build).Match(text);
        if (!match.Success)
        {
            return null;
        }

        var values = new List<string>();
        for (var index = 0; match.Groups[GroupName(index)] is { Success: true } group; index++)
        {
            values.Add(group.Value);
        }

        return values;
    }

    private static Regex Build(string format) =>
        new("^" + Placeholder().Replace(Regex.Escape(format), placeholder => $"(?<{GroupName(int.Parse(placeholder.Groups[1].Value, CultureInfo.InvariantCulture))}>.*?)"),
            RegexOptions.CultureInvariant);

    private static string GroupName(int index) => string.Create(CultureInfo.InvariantCulture, $"p{index}");

    // Regex.Escape turns "{0}" into "\{0}".
    [GeneratedRegex(@"\\\{(\d+)}")]
    private static partial Regex Placeholder();
}
