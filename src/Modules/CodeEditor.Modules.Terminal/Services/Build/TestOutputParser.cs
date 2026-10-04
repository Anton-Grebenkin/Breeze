using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Parses English <c>dotnet test</c> output in two formats: Microsoft.Testing.Platform (<c>failed Name (35ms)</c>,
/// totals <c>total: 83</c>…) and VSTest (<c>Failed Name [35 ms]</c>, <c>Failed: 1, Passed: 82, Skipped: 0, Total: 83</c>).
/// Each failed test keeps up to <see cref="MaxDetailLines"/> lines, without framework stack frames. O(n) in lines.
/// </summary>
public static partial class TestOutputParser
{
    public const int MaxDetailLines = 8;

    private static readonly string[] FrameworkFrames = ["at System.", "at Xunit.", "at Microsoft.", "at NUnit.", "at --- End", "--- End of", "from "];

    public static TestReport Parse(string target, string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var failures = new List<FailedTest>();
        int total = 0, passed = 0, failed = 0, skipped = 0;
        var parsed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (FailedLine().Match(lines[i]) is { Success: true } failure)
            {
                failures.Add(new FailedTest(failure.Groups["name"].Value.Trim(), Details(lines, i + 1)));
            }
            else if (VsTestSummary().Match(lines[i]) is { Success: true } summary)
            {
                (failed, passed, skipped, total, parsed) = (failed + Number(summary, "failed"), passed + Number(summary, "passed"), skipped + Number(summary, "skipped"), total + Number(summary, "total"), true);
            }
            else if (MtpCounter().Match(lines[i]) is { Success: true } counter)
            {
                parsed = true;
                var value = Number(counter, "value");
                switch (counter.Groups["name"].Value)
                {
                    case "total": total += value; break;
                    case "failed": failed += value; break;
                    case "succeeded": passed += value; break;
                    case "skipped": skipped += value; break;
                }
            }
        }

        var buildErrors = MsBuildOutputParser.Parse(output).Where(diagnostic => diagnostic.IsError).ToList();
        return new TestReport(target, total, passed, failed, skipped, failures, buildErrors, parsed);
    }

    private static List<string> Details(string[] lines, int start)
    {
        var details = new List<string>();
        for (var i = start; i < lines.Length && details.Count < MaxDetailLines; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || !char.IsWhiteSpace(line[0]) || FailedLine().IsMatch(line) || MtpCounter().IsMatch(line))
            {
                break;
            }

            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !FrameworkFrames.Any(frame => trimmed.StartsWith(frame, StringComparison.Ordinal)))
            {
                details.Add(trimmed);
            }
        }

        return details;
    }

    private static int Number(Match match, string group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\s*(?:failed|Failed)\s+(?<name>\S.*?)\s+[\(\[](?:\d+\s*[a-zµ]+\s*)+[\)\]]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FailedLine();

    [GeneratedRegex(@"^\s*(?<name>total|failed|succeeded|skipped):\s*(?<value>\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex MtpCounter();

    [GeneratedRegex(@"Failed:\s*(?<failed>\d+),\s*Passed:\s*(?<passed>\d+),\s*Skipped:\s*(?<skipped>\d+),\s*Total:\s*(?<total>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex VsTestSummary();
}
