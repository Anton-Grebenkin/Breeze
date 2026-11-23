using System.Text.Json;
using CodeEditor.Shell.Services;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Web;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Reading;
using CodeEditor.Modules.Browser.Services.Tabs;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Browser.Services.Web;

/// <summary>
/// The web through the editor's browser (ADR 0054) for the agent's <c>web_search</c> and <c>web_fetch</c>: the results
/// of the search engine from <c>browser.searchEngine</c> and pages a plain request cannot get. It works in a tab of the
/// agent's kept for this, behind the user's pages. A results page that asks for a person — a captcha, a consent window
/// — is shown to the user with a notification, and the search waits for them.
/// </summary>
public sealed class BrowserWebAccess(BrowserTabs tabs, IOptionsMonitor<BrowserOptions> options, INotificationService? notifications = null, Func<Task>? showPanel = null)
    : IWebBrowserAccess
{
    public const string NotificationId = "browser.search";

    /// <summary>How long the search waits for the user at a captcha.</summary>
    public static readonly TimeSpan UserWait = TimeSpan.FromSeconds(90);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private BrowserTab? _tab;

    /// <summary>The wait between looks at a page that needs the user; tests make it instant.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    public async Task<IReadOnlyList<WebSearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var address = BrowserSearchEngines.Url(options.CurrentValue.SearchEngine, query);
        var tab = Tab();
        try
        {
            await tab.Page.NavigateAsync(address, cancellationToken);
            var (hits, needsUser) = await HitsAsync(tab, cancellationToken);
            return hits.Count == 0 && needsUser ? await WaitForUserAsync(tab, cancellationToken) : hits;
        }
        catch (BrowserException exception)
        {
            throw new AgentToolException(exception.Message);
        }
    }

    public async Task<string> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        var tab = Tab();
        try
        {
            await tab.Page.NavigateAsync(url, cancellationToken);
            using var reply = JsonDocument.Parse(await tab.Page.EvaluateAsync(BrowserReading.Markdown, cancellationToken));
            return reply.RootElement.TryGetProperty("text", out var text) && text.GetString() is { Length: > 0 } markdown ? markdown : Strings.EmptyPage;
        }
        catch (BrowserException exception)
        {
            throw new AgentToolException(exception.Message);
        }
    }

    private BrowserTab Tab() => _tab is { } tab && tabs.Tabs.Contains(tab) ? tab : _tab = tabs.Open(BrowserTabOwner.Agent, select: false);

    // The page is shown to the user; the search goes on as soon as results appear, or gives up after the wait.
    private async Task<IReadOnlyList<WebSearchHit>> WaitForUserAsync(BrowserTab tab, CancellationToken cancellationToken)
    {
        tabs.Select(tab);
        if (showPanel is not null)
        {
            await showPanel();
        }

        using var notice = notifications?.Show(new Notification(NotificationId, Strings.SearchNeedsUserNotification, []));
        for (var waited = TimeSpan.Zero; waited < UserWait; waited += PollInterval)
        {
            await Delay(PollInterval, cancellationToken);
            var (hits, _) = await HitsAsync(tab, cancellationToken);
            if (hits.Count > 0)
            {
                return hits;
            }
        }

        throw new AgentToolException(Strings.SearchNeedsUser);
    }

    private static async Task<(IReadOnlyList<WebSearchHit> Hits, bool NeedsUser)> HitsAsync(BrowserTab tab, CancellationToken cancellationToken)
    {
        using var reply = JsonDocument.Parse(await tab.Page.EvaluateAsync(BrowserSearchScript.Results, cancellationToken));
        var root = reply.RootElement;
        var hits = root.TryGetProperty("hits", out var found) && found.ValueKind == JsonValueKind.Array
            ? found.EnumerateArray().Select(static hit => new WebSearchHit(Text(hit, "title"), Text(hit, "url"), Text(hit, "snippet"))).Where(static hit => hit.Url.Length > 0).ToList()
            : [];
        return (hits, root.TryGetProperty("needsUser", out var needs) && needs.ValueKind == JsonValueKind.True);
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
