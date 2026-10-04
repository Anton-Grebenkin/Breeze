namespace CodeEditor.Modules.Documents.Services;

/// <summary>Documents module settings: the <c>documents</c> section of settings.json.</summary>
public sealed class DocumentOptions
{
    public const string Section = "documents";

    /// <summary>
    /// <c>document_change</c> actions the agent performs without a card ("Always allow" on the card). Replacing an existing
    /// file (<c>overwrite</c>) still asks (<see cref="DocumentApprovals"/>).
    /// </summary>
    public List<string> AlwaysAllow { get; set; } = [];
}
