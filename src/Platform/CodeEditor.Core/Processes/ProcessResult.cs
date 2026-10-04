namespace CodeEditor.Core.Processes;

/// <summary>Process outcome: exit code, output (stdout and stderr in arrival order), timeout flag, duration.</summary>
/// <param name="Output">
/// Head and tail of the output; the middle of long output is dropped (<see cref="BoundedOutput"/>).
/// </param>
public sealed record ProcessResult(int ExitCode, string Output, bool TimedOut, TimeSpan Elapsed);
