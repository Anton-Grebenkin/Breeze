namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>What a background job (a build or test run) leaves: the exit code and the report for the model.</summary>
public readonly record struct JobResult(int ExitCode, string Report);
