namespace CodeEditor.Modules.Agent.Contracts.Approvals;

public enum ProposedChangeKind
{
    Edit,
    Create,
    Delete,
    Move,

    /// <summary>A shell command: the path is the working folder, the new text is the command itself.</summary>
    Command,
}
