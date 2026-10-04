namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>Test run result over one or more projects.</summary>
/// <param name="Parsed">Totals were found in the output; otherwise the tests most likely didn't start.</param>
public sealed record TestReport(
    string Target,
    int Total,
    int Passed,
    int Failed,
    int Skipped,
    IReadOnlyList<FailedTest> Failures,
    IReadOnlyList<BuildDiagnostic> BuildErrors,
    bool Parsed)
{
    public TimeSpan Elapsed { get; init; }

    public bool TimedOut { get; init; }

    public string OutputTail { get; init; } = string.Empty;

    public bool Succeeded => Parsed && Failed == 0 && BuildErrors.Count == 0 && !TimedOut;
}
