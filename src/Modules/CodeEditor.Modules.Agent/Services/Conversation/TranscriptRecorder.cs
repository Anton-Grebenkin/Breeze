using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// The agent's last model request, for the Deep mode advisor (ADR 0012), so it sees the work exactly as the model did.
/// Stores a copy of the message list; safe to read from any thread.
/// </summary>
public sealed class TranscriptRecorder
{
    private ChatMessage[] _last = [];

    public IReadOnlyList<ChatMessage> LastRequest => Volatile.Read(ref _last);

    public void Record(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        Volatile.Write(ref _last, [.. messages]);
    }

    public void Reset() => Volatile.Write(ref _last, []);
}
