namespace CodeEditor.Core.Commands;

/// <summary>A command's handler threw (<see cref="ICommandService.Failed"/>).</summary>
public sealed class CommandFailedEventArgs(CommandDefinition command, Exception exception) : EventArgs
{
    public CommandDefinition Command { get; } = command;

    public Exception Exception { get; } = exception;
}
