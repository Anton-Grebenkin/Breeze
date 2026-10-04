using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>Helper model client that reports token usage to <see cref="HelperUsage"/>.</summary>
internal sealed class UsageCountingChatClient(IChatClient inner, HelperUsage usage) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        Count(response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var accumulator = new UsageAccumulator();
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (var content in update.Contents.OfType<UsageContent>())
            {
                Count(accumulator.Add(content.Details));
            }

            yield return update;
        }

        Count(accumulator.Flush());
    }

    private void Count(UsageDetails? details)
    {
        if (details is not null)
        {
            usage.Add(details.InputTokenCount ?? 0, details.OutputTokenCount ?? 0, details.CachedInputTokenCount ?? 0);
        }
    }
}
