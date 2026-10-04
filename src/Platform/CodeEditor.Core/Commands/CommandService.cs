using System.Diagnostics;
using CodeEditor.Core.Context;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Commands;

/// <summary>
/// Executes registered commands, honoring their context conditions. A failing handler never crashes the UI.
/// </summary>
public sealed partial class CommandService(
    ICommandRegistry registry,
    IContextKeyService context,
    ILogger<CommandService> logger) : ICommandService
{
    public bool CanExecute(string commandId) =>
        registry.TryGet(commandId, out var command) && context.Evaluate(command.When);

    public async ValueTask<CommandExecutionStatus> ExecuteAsync(
        string commandId,
        object? argument = null,
        CancellationToken cancellationToken = default)
    {
        if (!registry.TryGet(commandId, out var command))
        {
            LogNotFound(logger, commandId);
            return CommandExecutionStatus.NotFound;
        }

        if (!context.Evaluate(command.When))
        {
            LogDisabled(logger, commandId);
            return CommandExecutionStatus.Disabled;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await command.Handler(argument, cancellationToken);
            LogExecuted(logger, commandId, ElapsedMs(started));
            return CommandExecutionStatus.Succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogCanceled(logger, commandId, ElapsedMs(started));
            return CommandExecutionStatus.Canceled;
        }
#pragma warning disable CA1031 // By design: one failing command must not crash the app.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogFailed(logger, exception, commandId);
            return CommandExecutionStatus.Failed;
        }
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    // Every command goes to the log file (Debug) to show what the user did before a crash.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Command {CommandId} executed in {ElapsedMs} ms")]
    private static partial void LogExecuted(ILogger logger, string commandId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Command {CommandId} canceled after {ElapsedMs} ms")]
    private static partial void LogCanceled(ILogger logger, string commandId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Command {CommandId} is not available in the current context")]
    private static partial void LogDisabled(ILogger logger, string commandId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Command {CommandId} not found")]
    private static partial void LogNotFound(ILogger logger, string commandId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command {CommandId} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, string commandId);
}
