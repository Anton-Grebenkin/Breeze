namespace CodeEditor.Core.Commands;

/// <summary>
/// Executes commands by id. The single entry point for menus, keybindings, the palette and the agent.
/// </summary>
public interface ICommandService
{
    /// <summary>True when the command is registered and its <c>when</c> condition holds.</summary>
    bool CanExecute(string commandId);

    /// <summary>Executes a command. Handler errors are logged, not thrown.</summary>
    ValueTask<CommandExecutionStatus> ExecuteAsync(
        string commandId,
        object? argument = null,
        CancellationToken cancellationToken = default);
}
