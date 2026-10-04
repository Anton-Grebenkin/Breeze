using System.Globalization;

namespace CodeEditor.Modules.Agent.Services.Cache;

/// <summary>
/// Conversation key for the provider cache: the cache lives on a specific server and the key routes one chat's requests
/// to one server (<c>prompt_cache_key</c> for OpenAI, the <c>x-grok-conv-id</c> header for xAI). Without it about one
/// in nine GPT and Grok requests missed the cache entirely. A new chat gets a new key.
/// </summary>
public sealed class PromptCacheKey
{
    private const string Prefix = "codeeditor-";

    private string _current = NewKey();

    public string Current => Volatile.Read(ref _current);

    /// <summary>A new or reopened chat has its own request prefix and its own cache.</summary>
    public void Renew() => Volatile.Write(ref _current, NewKey());

    private static string NewKey() => Prefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
}
