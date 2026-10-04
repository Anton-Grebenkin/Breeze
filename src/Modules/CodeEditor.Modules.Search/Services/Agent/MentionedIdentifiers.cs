using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Search.Services.Agent;

/// <summary>
/// Code names in user text: CamelCase, snake_case, dotted names (<c>v.category_record_id</c>, <c>Program.cs</c>)
/// and backtick-quoted text. Plain words lack case changes, underscores and dots, so they don't match.
/// Results keep text order, without duplicates.
/// </summary>
public static partial class MentionedIdentifiers
{
    public const int MaxIdentifiers = 6;
    public const int MinLength = 4;
    public const int MaxLength = 80;

    public static IReadOnlyList<string> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Identifier().Matches(text))
        {
            var identifier = (match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Value).Trim();
            if (identifier.Length is >= MinLength and <= MaxLength && !identifier.Contains('\n', StringComparison.Ordinal) && seen.Add(identifier))
            {
                found.Add(identifier);
                if (found.Count == MaxIdentifiers)
                {
                    break;
                }
            }
        }

        return found;
    }

    // Alternative order matters: quoted text wins as a whole, dotted names before their parts.
    [GeneratedRegex(
        @"`(?<quoted>[^`\r\n]+)`" +
        @"|\b[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+\b" +
        @"|\b[A-Za-z][a-z0-9]+(?:[A-Z][A-Za-z0-9]*)+\b" +
        @"|\b[A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
}
