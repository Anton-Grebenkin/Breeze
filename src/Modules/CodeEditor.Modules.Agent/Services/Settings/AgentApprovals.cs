namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// What the agent does without asking (<c>agent.approvals</c>). File deletion and commands always need approval: they
/// cannot be undone with <c>Ctrl+Z</c>.
/// </summary>
public enum AgentApprovals
{
    /// <summary>Every edit is a card with a diff and buttons.</summary>
    Edits,

    /// <summary>
    /// Edits, file creation and moves apply at once, as in Copilot: the card stays as a record, revert is in the
    /// changes panel.
    /// </summary>
    Auto,
}
