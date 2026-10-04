using System.Collections.Concurrent;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Resources;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// The agent's background commands (ADR 0012): at most <see cref="MaxRunning"/> at once, numbered sequentially.
/// All stop with their child processes on workspace change and exit, so no orphaned <c>dotnet run</c> is left behind.
/// </summary>
public sealed partial class BackgroundCommands : IDisposable
{
    public const int MaxRunning = 4;

    private readonly IProcessRunner _runner;
    private readonly IWorkspace _workspace;
    private readonly TimeProvider _time;
    private readonly ILogger<BackgroundCommands> _logger;
    private readonly ConcurrentDictionary<int, BackgroundCommand> _commands = new();
    private int _nextId;

    public BackgroundCommands(IProcessRunner runner, IWorkspace workspace, TimeProvider time, ILogger<BackgroundCommands> logger)
    {
        _runner = runner;
        _workspace = workspace;
        _time = time;
        _logger = logger;
        _workspace.Changed += OnWorkspaceChanged;
    }

    /// <summary>The chat's commands by id, running and finished.</summary>
    public IReadOnlyList<BackgroundCommand> All => [.. _commands.Values.OrderBy(command => command.Id)];

    /// <exception cref="AgentToolException"><see cref="MaxRunning"/> background commands are already running.</exception>
    public BackgroundCommand Start(ProcessRequest request, string command, Action<string> onLine)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onLine);
        if (_commands.Values.Count(running => running.IsRunning) >= MaxRunning)
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.BackgroundLimit, MaxRunning));
        }

        var started = new BackgroundCommand(Interlocked.Increment(ref _nextId), command, request.WorkingDirectory, _time.GetUtcNow());
        _commands[started.Id] = started;
        _ = RunAsync(started, request with { Timeout = Timeout.InfiniteTimeSpan }, onLine);
        return started;
    }

    /// <exception cref="AgentToolException">No command with this id.</exception>
    public BackgroundCommand Get(int id) =>
        _commands.TryGetValue(id, out var command)
            ? command
            : throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.BackgroundNotFound, id));

    public void Stop(int id) => Get(id).Cancellation.Cancel();

    /// <summary>Drops a command that finished while <c>run_command</c> waited: it never went to the background.</summary>
    public void Forget(int id)
    {
        if (_commands.TryRemove(id, out var command))
        {
            command.Dispose();
        }
    }

    public TimeSpan Elapsed(BackgroundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return (command.Finished ?? _time.GetUtcNow()) - command.Started;
    }

    /// <summary>Report for the model: status line, how to continue, new output.</summary>
    public string Report(BackgroundCommand command, string newOutput) => BackgroundCommandText.Report(command, Elapsed(command), newOutput);

    public void StopAll()
    {
        foreach (var command in _commands.Values.Where(command => command.IsRunning))
        {
            command.Cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        StopAll();
    }

    private async Task RunAsync(BackgroundCommand command, ProcessRequest request, Action<string> onLine)
    {
        try
        {
            var result = await _runner.RunAsync(request, line =>
            {
                command.Append(line);
                onLine(line);
            }, command.Cancellation.Token);
            command.Finish(result.ExitCode, stopped: false, _time.GetUtcNow());
        }
        catch (OperationCanceledException)
        {
            command.Finish(null, stopped: true, _time.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            command.Append(exception.Message);
            command.Finish(null, stopped: false, _time.GetUtcNow());
        }

        LogFinished(_logger, command.Id, command.ExitCode, command.IsStopped);
    }

    // Another folder is another project: the old background processes are no longer needed.
    private void OnWorkspaceChanged(object? sender, EventArgs e) => StopAll();

    [LoggerMessage(Level = LogLevel.Information, Message = "Background command #{Id} finished: exit code {ExitCode}, stopped {Stopped}")]
    private static partial void LogFinished(ILogger logger, int id, int? exitCode, bool stopped);
}
