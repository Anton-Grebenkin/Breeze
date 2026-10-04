using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Cache;

/// <summary>
/// Conversation key (<see cref="PromptCacheKey"/>) in the request body, so the service routes one conversation's
/// requests to where their cache lives. The library lacks these fields, so <see cref="JsonBodyPolicy"/> adds them.
/// <list type="bullet">
/// <item><c>prompt_cache_key</c>, OpenAI Responses API: requests with one key and prefix go to the cached server;</item>
/// <item><c>session_id</c>, OpenRouter (which ProxyAPI uses for xAI models): requests with the same key go to the same
/// provider. The xAI header <c>x-grok-conv-id</c> apparently does not reach xAI.</item>
/// </list>
/// GPT-6 keeps past reasoning in context anyway (<c>reasoning.context</c> defaults to <c>all_turns</c>) when it is in
/// history.
/// </summary>
internal static class CacheKeyField
{
    public const string PromptCacheKey = "prompt_cache_key";

    public const string SessionId = "session_id";

    public static bool Mark(JsonNode body, string field, string key)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body is not JsonObject request || request.ContainsKey(field))
        {
            return false;
        }

        request[field] = key;
        return true;
    }
}
