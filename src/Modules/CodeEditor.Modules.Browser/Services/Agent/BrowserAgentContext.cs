using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>
/// Browser context for the agent: which sites are open, so "the page I opened" needs no explanation and the agent
/// reads it instead of asking, and which sites the browser knows, so "open GitLab" needs no address. Sent while the
/// user keeps the tabs chip on (<see cref="BrowserTabsSwitch"/>).
/// </summary>
public sealed class BrowserAgentContext(BrowserTabsSwitch tabs, BrowserKnownSites? sites = null) : IAgentContextProvider
{
    /// <summary>Known sites named in the context: the most recently used ones.</summary>
    public const int MaxSites = 12;

    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        if (!tabs.IsOn)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>([]);
        }

        var lines = BrowserTabLines.Describe(tabs.Tabs());
        return ValueTask.FromResult<IReadOnlyList<string>>(sites?.Hosts is { Count: > 0 } hosts
            ? [.. lines, string.Format(CultureInfo.CurrentCulture, Strings.ContextKnownSites, string.Join(", ", hosts.Take(MaxSites)))]
            : lines);
    }
}
