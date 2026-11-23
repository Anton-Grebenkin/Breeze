using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>A fake browser: its pages are <see cref="FakeBrowserPage"/>, kept in the order they were made.</summary>
internal sealed class FakeBrowserEngine : IBrowserEngine
{
    public List<FakeBrowserPage> Pages { get; } = [];

    public IBrowserPage CreatePage(BrowserPageState? restored = null)
    {
        var page = new FakeBrowserPage(restored);
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
}
