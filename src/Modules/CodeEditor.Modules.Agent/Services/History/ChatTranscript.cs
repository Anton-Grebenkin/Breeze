using System.Text.Json;

namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>
/// A saved chat in <c>.breeze/agent/</c>: the panel feed, the agent session (history for the model) and token usage.
/// </summary>
public sealed record ChatTranscript(string Id, string Title, DateTimeOffset Created, List<ChatTranscriptMessage> Messages)
{
    /// <summary>Last answer time; missing in older files, where the creation time is used.</summary>
    public DateTimeOffset? Updated { get; init; }

    public ContextUsage? Usage { get; init; }

    public JsonElement? Session { get; init; }

    public ChatSummary ToSummary() => new(Id, Title, Created, Updated ?? Created);
}
