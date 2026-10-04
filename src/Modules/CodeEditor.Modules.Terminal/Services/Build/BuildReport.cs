namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>Build result: target, success, diagnostics, duration and the output tail for unexplained failures.</summary>
public sealed record BuildReport(string Target, bool Succeeded, IReadOnlyList<BuildDiagnostic> Diagnostics, TimeSpan Elapsed, DateTimeOffset Finished)
{
    public string OutputTail { get; init; } = string.Empty;

    public bool TimedOut { get; init; }

    public int Errors => Diagnostics.Count(diagnostic => diagnostic.IsError);

    public int Warnings => Diagnostics.Count(diagnostic => !diagnostic.IsError);
}
