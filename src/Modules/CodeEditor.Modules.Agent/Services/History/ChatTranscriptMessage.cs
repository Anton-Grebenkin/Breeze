namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>A feed row; an approval card is stored as "title — decision" text.</summary>
public sealed record ChatTranscriptMessage(ChatMessageKind Kind, string Text)
{
    /// <summary>What was attached to the question as context (the open file).</summary>
    public string? Attachment { get; init; }

    /// <summary>Names of files attached to the question.</summary>
    public IReadOnlyList<string>? Files { get; init; }

    /// <summary>Icon and outcome of a tool row; absent in chats before ADR 0010.</summary>
    public ChatTranscriptTool? Tool { get; init; }
}
