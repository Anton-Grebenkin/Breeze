using CodeEditor.Core.Commands;

namespace CodeEditor.Modules.Diagrams.Tests.Infrastructure;

/// <summary>Commands without a registry: records calls with their arguments.</summary>
internal sealed class FakeCommandService : ICommandService
{
    public List<(string Id, object? Argument)> Executed { get; } = [];

    public bool CanExecute(string commandId) => true;

    public ValueTask<CommandExecutionStatus> ExecuteAsync(string commandId, object? argument = null, CancellationToken cancellationToken = default)
    {
        Executed.Add((commandId, argument));
        return ValueTask.FromResult(CommandExecutionStatus.Succeeded);
    }
}
