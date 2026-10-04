using CodeEditor.Core.Processes;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// docker without docker: answers by command ("ps", "images", "stop", "compose config", "compose up") and records
/// requests. Output is delivered line by line like a real process; a held command "runs" until the test releases it or
/// cancels the request.
/// </summary>
internal sealed class ScriptedDocker : IProcessRunner
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (int ExitCode, string Output)> _answers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _holds = new(StringComparer.Ordinal);
    private readonly List<ProcessRequest> _requests = [];

    /// <summary>Sends more lines for a held command, the way a streamed container log arrives.</summary>
    public Action<string>? HeldOutput { get; private set; }

    public IReadOnlyList<ProcessRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Request commands in order: "ps", "images", "compose config".</summary>
    public IReadOnlyList<string> Commands => [.. Requests.Select(request => Key(request.Arguments))];

    public static string Key(IReadOnlyList<string> arguments) =>
        arguments[0] == "compose" ? "compose " + arguments[2] : arguments[0] is "image" or "container" ? arguments[0] + " " + arguments[1] : arguments[0];

    public ScriptedDocker Answer(string command, string output, int exitCode = 0)
    {
        lock (_gate)
        {
            _answers[command] = (exitCode, output);
        }

        return this;
    }

    /// <summary>Holds the command until the returned task completes.</summary>
    public TaskCompletionSource Hold(string command)
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _holds[command] = hold;
        }

        return hold;
    }

    public int Count(string command) => Commands.Count(candidate => candidate == command);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onLine, CancellationToken cancellationToken)
    {
        var key = Key(request.Arguments);
        (int ExitCode, string Output) answer;
        TaskCompletionSource? hold;
        lock (_gate)
        {
            _requests.Add(request);
            answer = _answers.GetValueOrDefault(key, (0, string.Empty));
            hold = _holds.GetValueOrDefault(key);
        }

        foreach (var line in answer.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            onLine?.Invoke(line);
        }

        if (hold is not null)
        {
            HeldOutput = onLine;
            await hold.Task.WaitAsync(cancellationToken);
        }

        return new ProcessResult(answer.ExitCode, answer.Output, TimedOut: false, TimeSpan.FromMilliseconds(5));
    }
}
