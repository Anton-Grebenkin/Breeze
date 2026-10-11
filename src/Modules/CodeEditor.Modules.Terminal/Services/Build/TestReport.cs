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

    /// <summary>No test ran: the filter matched nothing. Not a success: nothing was checked.</summary>
    public bool NoTests => Parsed && Total == 0 && BuildErrors.Count == 0 && !TimedOut;

    public bool Succeeded => Parsed && Total > 0 && Failed == 0 && BuildErrors.Count == 0 && !TimedOut;
}
