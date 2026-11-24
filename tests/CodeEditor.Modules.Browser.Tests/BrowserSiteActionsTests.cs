using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Acting on a site (ADR 0054): in its own tab the agent reads and follows links freely, but a button or a field on a
/// site that is not this machine needs a card that names it — "Sync" in Argo CD — until the user allows the site.
/// </summary>
public sealed class BrowserSiteActionsTests
{
    private const string Snapshot = "- heading \"shop\"\n- link \"Журнал\" [ref=e1] -> /logs\n- table (2 rows)\n  | shop | button \"Sync\" [ref=e2] |\n  | link \"blog\" [ref=e3] -> /blog | button \"Sync\" [ref=e4] |\n- textbox \"Поиск\" [ref=e5]";

    private readonly BrowserAgentTabs _tabs;
    private readonly FakeBrowserPage _browser;
    private readonly TestOptionsMonitor<BrowserOptions> _options = new(new BrowserOptions());
    private readonly FakeSettingsService _settings = new();
    private readonly BrowserApprovals _approvals;
    private readonly BrowserAgentTools _tools;

    public BrowserSiteActionsTests()
    {
        _tabs = FakeBrowserEngine.AgentTabs(out _browser);
        _browser.Load("https://argocd.example.com/applications/shop", "shop - Argo CD");
        _browser.SnapshotResult = JsonSerializer.Serialize(new { title = "shop - Argo CD", url = "https://argocd.example.com/applications/shop", text = Snapshot, truncated = false });
        _approvals = new BrowserApprovals(_options, _tabs, _settings, NullLogger<BrowserApprovals>.Instance);
        _tools = new BrowserAgentTools(_tabs, new PassThroughOutputStore(), new NoImages());
    }

    [Fact]
    public async Task ButtonOnASite_NeedsACardThatNamesIt_LinksAndReadingDoNot()
    {
        await SnapshotAsync();
        _browser.ScriptReplies[BrowserScripts.Describe("e2")] = new Queue<string>(["""{"ok":true,"role":"button","name":"Sync"}"""]);

        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", "e2")));
        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", "e4")));
        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("type", "e5")));
        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("press", null)));

        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", "e1")));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", "e3")));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("snapshot", null)));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("hover", "e2")));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("scroll", "e2")));

        var card = Assert.Single(await _tools.PreviewAsync(BrowserAgentTools.ToolName, Call("click", "e2"), TestContext.Current.CancellationToken));
        Assert.Equal(("Агент хочет действовать на сайте", "argocd.example.com", "Нажать: «Sync» (button)"), (card.Title, card.Header, card.NewText));
    }

    // A ref the agent has not seen in a snapshot is not known to be a link: it asks.
    [Fact]
    public void ClickBeforeAnySnapshot_Asks() =>
        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", "e1")));

    [Fact]
    public async Task AlwaysAllow_AddsTheSiteToThoseTheAgentActsOn()
    {
        await SnapshotAsync();

        var rule = _approvals.SuggestRule(BrowserAgentTools.ToolName, Call("click", "e2"));
        _approvals.AllowAlways(BrowserAgentTools.ToolName, rule!);

        Assert.Equal("действия на argocd.example.com", rule);
        Assert.Equal(new[] { "argocd.example.com" }, _settings.Written[BrowserApprovals.ActHostsKey]);
        Assert.False(_settings.Written.ContainsKey(BrowserApprovals.AllowedHostsKey));

        _options.Set(new BrowserOptions { ActHosts = ["argocd.example.com"] });
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", "e2")));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("type", "e5")));
    }

    // "Allow for this chat" on a browser card covers pressing and typing on sites and opening new ones for the rest
    // of the chat; a file sent to a site and a request that changes something still ask every time.
    [Fact]
    public void AllowForThisChat_CoversActionsOnSites_ButNotFilesAndChangingRequests()
    {
        Assert.False(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, Call("click", "e2")));
        Assert.False(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, Call("type", "e5")));
        Assert.False(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, new Dictionary<string, object?> { ["action"] = "navigate", ["url"] = "https://new.example.org/" }));

        Assert.True(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, new Dictionary<string, object?> { ["action"] = "upload", ["ref"] = "e5", ["path"] = "a.csv" }));
        Assert.True(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, new Dictionary<string, object?> { ["action"] = "request", ["method"] = "POST", ["url"] = "/api/v1/applications/shop/sync" }));
    }

    // The app the user develops: nothing to ask.
    [Fact]
    public void PageOfThisMachine_ActsAtOnce()
    {
        _browser.Load("http://localhost:5000/orders", "Заказы");

        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", "e2")));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("type", "e5")));
        Assert.Null(_approvals.SuggestRule(BrowserAgentTools.ToolName, Call("click", "e2")));
    }

    private async Task SnapshotAsync() =>
        await _tools.CreateTools().OfType<AIFunction>().Single().InvokeAsync(new AIFunctionArguments { ["action"] = "snapshot" }, TestContext.Current.CancellationToken);

    // Ask and Plan: the agent looks at pages and moves between them; acting on one waits for a mode that changes things.
    [Theory]
    [InlineData("snapshot", true)]
    [InlineData("read", true)]
    [InlineData("find", true)]
    [InlineData("screenshot", true)]
    [InlineData("network", true)]
    [InlineData("tabs", true)]
    [InlineData("navigate", true)]
    [InlineData("open_tab", true)]
    [InlineData("scroll", true)]
    [InlineData("click", false)]
    [InlineData("type", false)]
    [InlineData("press", false)]
    [InlineData("drag", false)]
    [InlineData("upload", false)]
    [InlineData("dialog", false)]
    [InlineData("evaluate", false)]
    [InlineData("made_up", false)]
    public void ReadingModes_LookAndMove_ButDoNotAct(string action, bool expected) =>
        Assert.Equal(expected, _approvals.ReadsInEveryMode(BrowserAgentTools.ToolName, Call(action, "e2")));

    [Fact]
    public void ReadingModes_RequestThatOnlyReads_Works_OneThatChangesDoesNot()
    {
        Assert.True(_approvals.ReadsInEveryMode(BrowserAgentTools.ToolName, Call("request", null)));
        Assert.True(_approvals.ReadsInEveryMode(BrowserAgentTools.ToolName, new Dictionary<string, object?> { ["action"] = "request", ["method"] = "get" }));
        Assert.False(_approvals.ReadsInEveryMode(BrowserAgentTools.ToolName, new Dictionary<string, object?> { ["action"] = "request", ["method"] = "POST" }));
        Assert.False(_approvals.ReadsInEveryMode("run_command", Call("snapshot", null)));
    }

    private static Dictionary<string, object?> Call(string action, string? reference) => new() { ["action"] = action, ["ref"] = reference };

    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }

    private sealed class NoImages : IAgentImages
    {
        public bool CanShow => false;

        public bool TryShow(string name, byte[] data, string mediaType) => false;
    }
}
