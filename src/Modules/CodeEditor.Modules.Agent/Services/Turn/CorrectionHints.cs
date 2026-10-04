using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Harness texts for agent self-checking (ADR 0008): verification gate reminders, correction after repeated failures,
/// and a note about unverified edits. Correction follows a metacognitive loop: error → cause class → false
/// assumption → targeted fix; on repeat, change the approach.
/// </summary>
public static class CorrectionHints
{
    private const int ListedFiles = 5;

    /// <returns>A hint appended to a build or test result after <paramref name="failuresInRow"/> failures in a row.</returns>
    public static string? AfterFailures(VerificationKind kind, int failuresInRow) => failuresInRow switch
    {
        < 2 => null,
        2 => "\n\n" + Format(Strings.HintSecondFailure, Failed(kind)),
        _ => "\n\n" + Format(Strings.HintRepeatedFailure, Failed(kind)),
    };

    /// <summary>Tells the model why the turn continues.</summary>
    public static string Reminder(VerificationGap gap, IReadOnlyCollection<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var changed = string.Join(", ", files.Take(ListedFiles));
        if (files.Count > ListedFiles)
        {
            changed = Format(Strings.ListAndMore, changed);
        }

        return (gap.Kind, gap.Failed) switch
        {
            (VerificationKind.Build, true) => Strings.ReminderBuildFailed,
            (VerificationKind.Build, false) => Format(Strings.ReminderBuildMissing, changed),
            (_, true) => Strings.ReminderTestsFailed,
            _ => Format(Strings.ReminderTestsMissing, changed),
        };
    }

    /// <summary>Feed line: what the harness is checking.</summary>
    public static string Status(VerificationGap gap) => (gap.Kind, gap.Failed) switch
    {
        (VerificationKind.Build, true) => Strings.CheckBuildFailed,
        (VerificationKind.Build, false) => Strings.CheckBuildMissing,
        (_, true) => Strings.CheckTestsFailed,
        _ => Strings.CheckTestsMissing,
    };

    /// <summary>Feed line: the turn ended with edits still unverified.</summary>
    public static string Unverified(VerificationGap gap) => (gap.Kind, gap.Failed) switch
    {
        (VerificationKind.Build, true) => Strings.UnverifiedBuildFailed,
        (VerificationKind.Build, false) => Strings.UnverifiedBuildMissing,
        (_, true) => Strings.UnverifiedTestsFailed,
        _ => Strings.UnverifiedTestsMissing,
    };

    private static string Failed(VerificationKind kind) => kind == VerificationKind.Build ? Strings.HintBuildFailing : Strings.HintTestsFailing;

    private static string Format(string format, string value) => string.Format(CultureInfo.CurrentCulture, format, value);
}
