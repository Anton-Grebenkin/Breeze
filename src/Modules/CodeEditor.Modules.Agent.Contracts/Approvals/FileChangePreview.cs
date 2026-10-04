namespace CodeEditor.Modules.Agent.Contracts.Approvals;

/// <summary>
/// What a tool will do to a file, for the approval card: old and new text (the chat builds the diff) and, for a move,
/// the new path.
/// </summary>
public sealed record FileChangePreview(ProposedChangeKind Kind, string RelativePath, string OldText, string NewText)
{
    public string? NewRelativePath { get; init; }

    /// <summary>
    /// Card title when the tool has its own meaning ("The agent wants to open a page"); <c>null</c> to derive it from
    /// the change kind.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>Caption of this change in the card ("Git commands", a site address); <c>null</c> to use kind and path.</summary>
    public string? Header { get; init; }
}
