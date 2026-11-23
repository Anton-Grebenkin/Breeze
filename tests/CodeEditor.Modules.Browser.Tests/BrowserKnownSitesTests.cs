using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Known sites (ADR 0054): a site the user opened, or let the agent open once, is remembered; the agent opens it
/// without a card and is told its name. A site the agent reached by a link is not known.
/// </summary>
public sealed class BrowserKnownSitesTests
{
    private static readonly string UserData = Path.GetFullPath(@"C:\user");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(UserData);
    private readonly BrowserTabs _tabs = new(new FakeBrowserEngine());
    private readonly BrowserAgentTabs _agent;
    private readonly BrowserKnownSites _sites;
    private readonly BrowserApprovals _approvals;
    private readonly AIFunction _tool;
    private readonly FakeBrowserPage _mine;

    public BrowserKnownSitesTests()
    {
        _agent = new BrowserAgentTabs(_tabs);
        _sites = Sites();
        _approvals = new BrowserApprovals(new TestOptionsMonitor<BrowserOptions>(new BrowserOptions()), _agent, new FakeSettingsService(), NullLogger<BrowserApprovals>.Instance, _sites);
        _tool = new BrowserAgentTools(_agent, new PassThroughOutputStore(), new NoImages(), sites: _sites).CreateTools().OfType<AIFunction>().Single();
        _mine = (FakeBrowserPage)_tabs.Open().Page;
    }

    [Fact]
    public async Task SiteTheUserOpened_IsKnown_TheAgentOpensItWithoutACard_AfterARestartToo()
    {
        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Navigate("https://gitlab.example.com/shop")));

        _mine.Load("https://gitlab.example.com/dashboard", "GitLab");
        _mine.Load("https://grafana.example.com/d/abc", "Grafana");

        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Navigate("https://gitlab.example.com/shop/-/pipelines")));
        Assert.Equal(["grafana.example.com", "gitlab.example.com"], _sites.Hosts);

        await _sites.Saved;
        using var reopened = Sites();
        Assert.Equal(_sites.Hosts, reopened.Hosts);
    }

    // The card was shown once and the user agreed: the call runs, and the site needs no card again.
    [Fact]
    public async Task SiteTheUserLetTheAgentOpenOnce_IsRemembered()
    {
        await InvokeAsync(Navigate("https://docs.example.org/guide"));

        Assert.True(_sites.IsKnown("docs.example.org"));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Navigate("https://docs.example.org/other")));
    }

    // A link the agent followed leads where the page wanted: that does not make the site known.
    [Fact]
    public void SiteTheAgentReachedByItself_IsNotKnown_NorIsThisMachine()
    {
        ((FakeBrowserPage)_agent.Open().Page).Load("https://tracker.example.net/landing", "Landing");
        _mine.Load("http://localhost:5000/", "Приложение");
        _mine.Load("about:blank", string.Empty);

        Assert.Empty(_sites.Hosts);
        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Navigate("https://tracker.example.net/?q=secret")));
    }

    [Fact]
    public async Task Agent_IsToldTheKnownSites_SoASiteIsOpenedByName()
    {
        _mine.Load("https://gitlab.example.com/dashboard", "GitLab");
        _mine.Load("https://argocd.example.com/applications", "Argo CD");
        using var chip = new BrowserTabsSwitch(_tabs, new TestOptionsMonitor<BrowserOptions>(new BrowserOptions()), new FakeSettingsService(), NullLogger<BrowserTabsSwitch>.Instance);

        var lines = await new BrowserAgentContext(chip, _sites).GetContextAsync(new AgentContextRequest(IncludeActiveEditor: false), TestContext.Current.CancellationToken);

        Assert.Equal("Сайты, которые пользователь уже открывал в браузере; они открываются без вопроса: argocd.example.com, gitlab.example.com.", lines[^1]);
    }

    private BrowserKnownSites Sites() => new(_tabs, _fileSystem, new UserDataPaths(UserData), NullLogger<BrowserKnownSites>.Instance);

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;

    private static Dictionary<string, object?> Navigate(string url) => new() { ["action"] = "navigate", ["url"] = url };

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
