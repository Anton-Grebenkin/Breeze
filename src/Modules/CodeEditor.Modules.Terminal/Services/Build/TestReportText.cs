using System.Globalization;
using System.Text;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Test result as text: a one-line summary for the status bar and channel, and details for the model: failed tests
/// with their gist (at most <see cref="MaxFailures"/>), build errors, and the output tail if there are no totals.
/// </summary>
public static class TestReportText
{
    public const int MaxFailures = 15;
    public const int TailLines = 40;

    public static string Summary(TestReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var seconds = Durations.Seconds(report.Elapsed);
        if (report.TimedOut)
        {
            return Format(Strings.TestsTimedOut, report.Target, seconds);
        }

        if (report.BuildErrors.Count > 0)
        {
            return Format(Strings.TestsNotStarted, report.Target, report.BuildErrors.Count);
        }

        return report.Parsed
            ? Format(Strings.TestsSummary, report.Target, report.Passed, report.Total, report.Failed, report.Skipped, seconds)
            : Format(Strings.TestsNoTotals, report.Target, seconds);
    }

    public static string Details(TestReport report, Func<string, string> relative)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(relative);
        var text = new StringBuilder(Summary(report));
        foreach (var failure in report.Failures.Take(MaxFailures))
        {
            text.Append("\n\n").Append(Format(Strings.TestFailed, failure.Name));
            foreach (var line in failure.Details)
            {
                text.Append("\n  ").Append(line);
            }
        }

        if (report.Failures.Count > MaxFailures)
        {
            text.Append('\n').Append(Format(Strings.MoreFailures, report.Failures.Count - MaxFailures));
        }

        foreach (var error in report.BuildErrors.Take(BuildReportText.MaxDiagnostics))
        {
            text.Append('\n').Append(error.Format(relative));
        }

        if (!report.Parsed && report.BuildErrors.Count == 0)
        {
            text.Append('\n').Append(Strings.OutputTail).Append('\n')
                .Append(string.Join('\n', report.OutputTail.TrimEnd().Split('\n').TakeLast(TailLines)));
        }

        return text.ToString();
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
