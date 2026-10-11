namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// What the agent does without asking (<c>agent.approvals</c>, only from the user's settings). In every mode a module
/// confirms what reaches outside or cannot be undone (<c>git push</c>, removing a container), agent.md can require a
/// card, and file deletion asks: <c>Ctrl+Z</c> doesn't bring a file back.
/// </summary>
public enum AgentApprovals
{
    /// <summary>Every edit is a card with a diff and buttons.</summary>
    Edits,

    /// <summary>
    /// Edits, file creation and moves apply at once, as in Copilot: the card stays as a record, revert is in the
    /// changes panel. Commands ask unless they only read or match a rule.
    /// </summary>
    Auto,

    /// <summary>
    /// Edits and commands run without asking, like Cursor's "Run everything" and Codex's full access: for a user who
    /// watches the feed and stops the agent when needed.
    /// </summary>
    Full,
}
