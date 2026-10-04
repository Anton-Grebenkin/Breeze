using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Prompts;

/// <summary>
/// Detects a request with several requirements (a rule list, API, examples, a bug report with expected results). Such
/// a turn needs an item-by-item check before the summary (<see cref="TurnChecks"/>): with a green build and its own
/// green tests a model confidently skips a requirement. A short request like "fix the failing test" needs no check.
/// One pass over the lines.
/// </summary>
public static partial class TaskRequirements
{
    /// <summary>This many list items already mean several requirements.</summary>
    public const int ListItems = 3;

    /// <summary>A request this long usually holds several conditions even without a list.</summary>
    public const int LongRequest = 600;

    /// <summary>This many "file:line" references in the summary mean the item check is already there.</summary>
    public const int EvidenceReferences = 2;

    public static bool AreSeveral(string? request)
    {
        if (string.IsNullOrWhiteSpace(request))
        {
            return false;
        }

        if (request.Length >= LongRequest)
        {
            return true;
        }

        var items = 0;
        foreach (var line in request.AsSpan().EnumerateLines())
        {
            if (IsListItem(line.TrimStart()) && ++items >= ListItems)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The summary already holds the check: code references (<c>src/A.cs:12</c>), not just "done".</summary>
    public static bool CitesEvidence(string report) =>
        !string.IsNullOrEmpty(report) && PathLine().Count(report) >= EvidenceReferences;

    // "- …", "* …", "• …", "1. …", "2) …".
    private static bool IsListItem(ReadOnlySpan<char> line)
    {
        if (line.Length > 1 && line[0] is '-' or '*' or '•' && line[1] == ' ')
        {
            return true;
        }

        var digits = 0;
        while (digits < line.Length && char.IsAsciiDigit(line[digits]))
        {
            digits++;
        }

        return digits is > 0 and <= 2 && digits + 1 < line.Length && line[digits] is '.' or ')' && line[digits + 1] == ' ';
    }

    [GeneratedRegex(@"[\w./\\-]+\.\w{1,6}:\d+")]
    private static partial Regex PathLine();
}
