using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Cache;

/// <summary>A model client that can extend the conversation cache by an hour (<see cref="CacheWarmupChatClient"/>).</summary>
internal interface IPromptCacheWarmer
{
    /// <summary>The model allows warm-up: budgeted reasoning cannot answer with a single token.</summary>
    bool CanWarmUp { get; }

    /// <summary>
    /// A request with the conversation's history and options, one-hour cache marks and a discarded one-token answer.
    /// </summary>
    /// <param name="messages">History, the model reply and a stub as the last message (left unmarked).</param>
    /// <returns>Request usage; <c>null</c> if the service did not report it.</returns>
    Task<UsageDetails?> WarmUpAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken);
}
