namespace CodeEditor.Core.Commands;

public enum CommandExecutionStatus
{
    Succeeded,

    /// <summary>No command with this id is registered.</summary>
    NotFound,

    /// <summary>The <c>when</c> condition is false in the current context.</summary>
    Disabled,

    Canceled,

    /// <summary>The handler threw; the exception is logged.</summary>
    Failed,
}
