using CodeEditor.Core.Processes;

namespace CodeEditor.Testing;

/// <summary>Process runner without processes: records requests and replays canned output line by line.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Queue<(int ExitCode, string Output)> _results = new();

    public List<ProcessRequest> Requests { get; } = [];

    /// <summary>
    /// Keeps the "process" running until the test completes this task, for background commands.
    /// Cancellation works as with a real process.
    /// </summary>
    public TaskCompletionSource? Hold { get; set; }

    public FakeProcessRunner Returns(int exitCode, string output)
    {
        _results.Enqueue((exitCode, output));
        return this;
    }

    public async Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onLine, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var (exitCode, output) = _results.Count > 0 ? _results.Dequeue() : (0, string.Empty);
        foreach (var line in output.Split('\n'))
        {
            onLine?.Invoke(line);
        }

        if (Hold is { } hold)
        {
            await hold.Task.WaitAsync(cancellationToken);
        }

        return new ProcessResult(exitCode, output, TimedOut: false, TimeSpan.FromSeconds(2));
    }
}
