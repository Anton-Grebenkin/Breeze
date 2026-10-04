namespace CodeEditor.Core.Processes;

/// <summary>Runs an external process without a window: builds, tests, agent commands. Faked in tests.</summary>
public interface IProcessRunner
{
    /// <param name="onLine">Each output line as it arrives (for the Output panel), on background threads.</param>
    /// <exception cref="OperationCanceledException">Canceled; the process tree was killed.</exception>
    /// <exception cref="InvalidOperationException">The program failed to start (not found).</exception>
    Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onLine, CancellationToken cancellationToken);
}
