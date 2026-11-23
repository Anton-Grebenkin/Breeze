using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>A fake browser: its pages are <see cref="FakeBrowserPage"/>, kept in the order they were made.</summary>
internal sealed class FakeBrowserEngine : IBrowserEngine
{
    public List<FakeBrowserPage> Pages { get; } = [];

    /// <summary>Sets up every page as it is made (preset script replies).</summary>
    public Action<FakeBrowserPage>? Configure { get; set; }

    public IBrowserPage CreatePage(BrowserPageState? restored = null, bool forAgent = false)
    {
        var page = new FakeBrowserPage(restored) { IsForAgent = forAgent };
        Configure?.Invoke(page);
        Pages.Add(page);
        return page;
    }

    /// <summary>Tabs over a fake browser with one open tab; <paramref name="page"/> is its page.</summary>
    public static BrowserTabs Tabs(out FakeBrowserPage page)
    {
        var tabs = new BrowserTabs(new FakeBrowserEngine());
        page = (FakeBrowserPage)tabs.Open().Page;
        return tabs;
    }

    /// <summary>The agent's view of a fake browser where the agent already has its tab; <paramref name="page"/> is its page.</summary>
    public static BrowserAgentTabs AgentTabs(out FakeBrowserPage page)
    {
        var tabs = new BrowserAgentTabs(new BrowserTabs(new FakeBrowserEngine()));
        page = (FakeBrowserPage)tabs.Open().Page;
        return tabs;
    }
}
