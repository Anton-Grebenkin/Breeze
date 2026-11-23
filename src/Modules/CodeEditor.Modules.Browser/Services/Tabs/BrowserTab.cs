namespace CodeEditor.Modules.Browser.Services.Tabs;

/// <summary>A browser tab: a page, who opened it and from which tab (a link "in a new window", a sign-in popup).</summary>
public sealed class BrowserTab
{
    internal BrowserTab(string id, IBrowserPage page, BrowserTabOwner owner, string? openerId)
    {
        Id = id;
        Page = page;
        Owner = owner;
        OpenerId = openerId;
    }

    /// <summary>Short id the agent names the tab by: <c>t1</c>, <c>t2</c>… Not reused within a run.</summary>
    public string Id { get; }

    public IBrowserPage Page { get; }

    public BrowserTabOwner Owner { get; }

    public string? OpenerId { get; }

    /// <summary>Pinned tabs stay first and survive "close other tabs".</summary>
    public bool IsPinned { get; internal set; }

    public bool IsAgent => Owner == BrowserTabOwner.Agent;

    /// <summary>An http(s) page is open in the tab (or kept from the last run).</summary>
    public bool HasWebPage => Page.Url is { Scheme: "http" or "https" };
}
