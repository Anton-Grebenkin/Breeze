using System.Text.Json;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Agent settings that the model client and the agent depend on: when they change, the conversation is rebuilt
/// (<see cref="AgentConversation.Invalidate"/>). Mode, approvals, the menu model list, auto memory and readable sites
/// apply per turn and do not rebuild the client; otherwise switching Ask → Agent cancelled a pending cache warm-up and
/// the next Claude turn missed the cache.
/// </summary>
public static class AgentClientSettings
{
    private static readonly string[] PerTurn =
    [
        nameof(AgentOptions.Mode),
        nameof(AgentOptions.Approvals),
        nameof(AgentOptions.Models),
        nameof(AgentOptions.AutoMemory),
        nameof(AgentOptions.WebHosts),
    ];

    /// <summary>Fingerprint of the client settings: if it matches, nothing needs rebuilding.</summary>
    public static string Of(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = JsonSerializer.SerializeToNode(options)!.AsObject();
        foreach (var name in PerTurn)
        {
            settings.Remove(name);
        }

        return settings.ToJsonString();
    }
}
