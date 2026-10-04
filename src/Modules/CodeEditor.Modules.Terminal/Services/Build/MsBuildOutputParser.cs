using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Parses MSBuild output: <c>file(line,col): error CS1002: text [project]</c> lines, also without a location
/// (<c>MSBUILD : error MSB1009: …</c>). Multi-target builds print an error once per target; duplicates are dropped.
/// O(n) in lines.
/// </summary>
public static partial class MsBuildOutputParser
{
    public static IReadOnlyList<BuildDiagnostic> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var diagnostics = new List<BuildDiagnostic>();
        var seen = new HashSet<(string?, int, int, string, string)>();
        foreach (var line in output.Split('\n'))
        {
            if (MayBeDiagnostic(line) && TryParse(line.TrimEnd('\r'), out var diagnostic)
                && seen.Add((diagnostic.File, diagnostic.Line, diagnostic.Column, diagnostic.Code, diagnostic.Message)))
            {
                diagnostics.Add(diagnostic);
            }
        }

        return diagnostics;
    }

    // Cheap vectorized pre-check: the regex only matches lines containing a severity word.
    private static bool MayBeDiagnostic(string line) =>
        line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("warning", StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string line, out BuildDiagnostic diagnostic)
    {
        var match = DiagnosticLine().Match(line);
        if (!match.Success)
        {
            diagnostic = null!;
            return false;
        }

        var origin = match.Groups["origin"].Value.Trim();
        var file = origin.Length == 0 || origin.Equals("MSBUILD", StringComparison.OrdinalIgnoreCase) || origin.StartsWith("CSC", StringComparison.OrdinalIgnoreCase) ? null : origin;
        diagnostic = new BuildDiagnostic(
            file,
            Number(match.Groups["line"]),
            Number(match.Groups["column"]),
            match.Groups["severity"].Value.Equals("error", StringComparison.OrdinalIgnoreCase),
            match.Groups["code"].Value,
            match.Groups["message"].Value.Trim());
        return true;
    }

    private static int Number(Group group) => group.Success ? int.Parse(group.Value, CultureInfo.InvariantCulture) : 0;

    // The trailing [project] is dropped: it repeats the file path.
    [GeneratedRegex(@"^\s*(?<origin>[^()\r\n]*?)(\((?<line>\d+)(,(?<column>\d+))?(,\d+,\d+)?\))?\s*:\s*(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<message>.*?)(\s+\[[^\]]+\])?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticLine();
}
