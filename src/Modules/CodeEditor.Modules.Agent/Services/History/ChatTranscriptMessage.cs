using System.Text.Json.Serialization;

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

    /// <summary>
    /// When the row appeared. With <see cref="Model"/> and <see cref="Mode"/> it lets a chat be studied afterwards: how
    /// long each step took and what ran it. Absent in chats saved before these fields.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? At { get; init; }

    /// <summary>On a question: the model that answered it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>On a question: the mode the turn ran in.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentMode? Mode { get; init; }
}
