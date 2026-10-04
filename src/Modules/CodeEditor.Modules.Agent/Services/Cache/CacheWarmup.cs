using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Cache;

/// <summary>
/// Chat cache warm-up (<see cref="CacheWarmupChatClient"/>): only where the cache uses marks with a TTL (Claude via
/// Anthropic Messages) and unless disabled in settings (<see cref="AgentOptions.CacheWarmup"/>).
/// </summary>
public sealed class CacheWarmup(HelperUsage usage, TimeProvider time, ILogger<CacheWarmup> logger)
{
    /// <returns>A warming client over the model; <c>null</c> if the model, service or settings do not allow warm-up.</returns>
    internal CacheWarmupChatClient? Wrap(IChatClient client, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        return options.CacheWarmup && client.GetService<IPromptCacheWarmer>() is { CanWarmUp: true } warmer
            ? new CacheWarmupChatClient(client, warmer, usage, time, logger)
            : null;
    }
}
