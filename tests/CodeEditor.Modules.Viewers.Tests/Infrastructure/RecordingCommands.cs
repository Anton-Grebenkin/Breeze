using CodeEditor.Core.Commands;

namespace CodeEditor.Modules.Viewers.Tests.Infrastructure;

/// <summary>Executed commands with their arguments.</summary>
internal sealed class RecordingCommands : ICommandService
{
    public List<(string Id, object? Argument)> Executed { get; } = [];

    public bool CanExecute(string commandId) => true;

    public ValueTask<CommandExecutionStatus> ExecuteAsync(string commandId, object? argument = null, CancellationToken cancellationToken = default)
    {
        Executed.Add((commandId, argument));
        return ValueTask.FromResult(CommandExecutionStatus.Succeeded);
    }
}
