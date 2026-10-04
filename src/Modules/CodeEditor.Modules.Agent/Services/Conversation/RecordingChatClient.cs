using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>Records the messages of each agent request in <see cref="TranscriptRecorder"/>; leaves the request unchanged.</summary>
internal sealed class RecordingChatClient(IChatClient inner, TranscriptRecorder recorder) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages as IList<ChatMessage> ?? [.. messages];
        recorder.Record(list);
        return base.GetResponseAsync(list, options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages as IList<ChatMessage> ?? [.. messages];
        recorder.Record(list);
        return base.GetStreamingResponseAsync(list, options, cancellationToken);
    }
}
