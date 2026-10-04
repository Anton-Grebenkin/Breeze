using System.Globalization;
using System.Text;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Build result as text: a one-line summary for the status bar and channel, and details for the model (errors
/// before warnings, at most <see cref="MaxDiagnostics"/>, workspace-relative paths). A failure without MSBuild-style
/// errors (dotnet itself failed) includes the output tail.
/// </summary>
public static class BuildReportText
{
    public const int MaxDiagnostics = 30;
    public const int TailLines = 40;

    public static string Summary(BuildReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var seconds = Durations.Seconds(report.Elapsed);
        if (report.TimedOut)
        {
            return Format(Strings.BuildTimedOut, report.Target, seconds);
        }

        if (!report.Succeeded)
        {
            return Format(Strings.BuildFailed, report.Target, seconds, report.Errors, report.Warnings);
        }

        return report.Warnings > 0
            ? Format(Strings.BuildSucceededWithWarnings, report.Target, seconds, report.Warnings)
            : Format(Strings.BuildSucceeded, report.Target, seconds);
    }

    public static string Details(BuildReport report, Func<string, string> relative)
    {
        ArgumentNullException.ThrowIfNull(report);
        var text = new StringBuilder(Summary(report));
        var ordered = report.Diagnostics.OrderBy(diagnostic => diagnostic.IsError ? 0 : 1).ToList();
        foreach (var diagnostic in ordered.Take(MaxDiagnostics))
        {
            text.Append('\n').Append(diagnostic.Format(relative));
        }

        if (ordered.Count > MaxDiagnostics)
        {
            text.Append('\n').Append(Format(Strings.MoreDiagnostics, ordered.Count - MaxDiagnostics));
        }

        if (!report.Succeeded && report.Errors == 0)
        {
            var tail = report.OutputTail.TrimEnd().Split('\n').TakeLast(TailLines);
            text.Append('\n').Append(Strings.BuildOutputTail).Append('\n').Append(string.Join('\n', tail));
        }

        return text.ToString();
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
