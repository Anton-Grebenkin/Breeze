using System.Collections.Frozen;
using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services.Web;

/// <summary>
/// Search engines for the agent's search through the browser (<c>browser.searchEngine</c>): a known name, or an
/// address of one's own with <c>{query}</c> where the query goes — a company search, a SearXNG instance.
/// </summary>
public static class BrowserSearchEngines
{
    public const string Default = "duckduckgo";
    public const string QueryMark = "{query}";

    private static readonly FrozenDictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["duckduckgo"] = "https://html.duckduckgo.com/html/?q={query}",
        ["bing"] = "https://www.bing.com/search?q={query}",
        ["google"] = "https://www.google.com/search?q={query}",
        ["yandex"] = "https://yandex.ru/search/?text={query}",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Names => Known.Keys;

    /// <summary>The results page for the query.</summary>
    /// <exception cref="AgentToolException">The setting is neither a known engine nor an http(s) address with <c>{query}</c>.</exception>
    public static Uri Url(string? engine, string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var name = string.IsNullOrWhiteSpace(engine) ? Default : engine.Trim();
        var template = Known.GetValueOrDefault(name, name);
        return template.Contains(QueryMark, StringComparison.Ordinal)
            && Uri.TryCreate(template.Replace(QueryMark, Uri.EscapeDataString(query.Trim()), StringComparison.Ordinal), UriKind.Absolute, out var url)
            && url.Scheme is "http" or "https"
                ? url
                : throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.SearchEngineInvalid, name, string.Join(", ", Known.Keys.Order(StringComparer.Ordinal))));
    }
}
