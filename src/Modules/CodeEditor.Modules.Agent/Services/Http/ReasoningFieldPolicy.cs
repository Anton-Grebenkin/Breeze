using System.ClientModel.Primitives;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>
/// Handles reasoning that a proxy sends outside <c>reasoning_content</c>: e.g. Grok via ProxyAPI sends a reasoning
/// summary in <c>reasoning</c> and an encrypted block in <c>reasoning_details</c> (OpenRouter format), while the library
/// reads only <c>reasoning_content</c>, so the feed stayed empty. The policy rewrites the Chat Completions stream
/// (<see cref="ReasoningFieldStream"/>): <c>reasoning</c> becomes <c>reasoning_content</c> and <c>reasoning_details</c>
/// is dropped (history does not need it). Think-aloud models get no summary: their tagged reasoning takes its place.
/// </summary>
/// <param name="keepReasoning"><c>false</c> to only drop the reasoning fields.</param>
internal sealed class ReasoningFieldPolicy(bool keepReasoning) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ArgumentNullException.ThrowIfNull(message);
        ProcessNext(message, pipeline, currentIndex);
        Rewrite(message);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ArgumentNullException.ThrowIfNull(message);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        Rewrite(message);
    }

    // Event streams only: whole responses (service requests) never reach the feed.
    private void Rewrite(PipelineMessage message)
    {
        if (message.Response is { ContentStream: { } content } response
            && response.Headers.TryGetValue("Content-Type", out var type)
            && type?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            response.ContentStream = new ReasoningFieldStream(content, keepReasoning);
        }
    }
}
