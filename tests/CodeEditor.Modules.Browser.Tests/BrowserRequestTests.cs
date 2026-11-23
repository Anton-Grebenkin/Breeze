using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// A request on behalf of a tab (ADR 0054): the site's API is called with the tab's sign-in. Reading a site the agent
/// may open is free, also in a tab of the user's; a request that changes something always asks, with the request shown.
/// </summary>
public sealed class BrowserRequestTests
{
    private readonly BrowserTabs _tabs = new(new FakeBrowserEngine());
    private readonly BrowserAgentTabs _agent;
    private readonly FakeBrowserPage _gitlab;
    private readonly TestOptionsMonitor<BrowserOptions> _options = new(new BrowserOptions { AllowedHosts = ["gitlab.example.com"] });
    private readonly BrowserApprovals _approvals;
    private readonly BrowserAgentTools _tools;

    public BrowserRequestTests()
    {
        _agent = new BrowserAgentTabs(_tabs);
        _gitlab = (FakeBrowserPage)_tabs.Open().Page;
        _gitlab.Load("https://gitlab.example.com/shop/-/pipelines", "Pipelines");
        _approvals = new BrowserApprovals(_options, _agent, new FakeSettingsService(), NullLogger<BrowserApprovals>.Instance);
        _tools = new BrowserAgentTools(_agent, new PassThroughOutputStore(), new NoImages());
    }

    [Fact]
    public async Task Request_ReturnsTheReply_WithStatusAndType()
    {
        _gitlab.DevToolsReplies["Runtime.evaluate"] =
            """{"result":{"type":"object","value":{"ok":true,"status":200,"type":"application/json","url":"https://gitlab.example.com/api/v4/projects/7/pipelines","body":"[{\"id\":41,\"status\":\"failed\"}]","cut":false}}}""";

        var result = await InvokeAsync(new() { ["action"] = "request", ["tab"] = "t1", ["url"] = "/api/v4/projects/7/pipelines" });

        Assert.Equal(
            "<browser_response tab=\"t1\" method=\"GET\" status=\"200\" type=\"application/json\" url=\"https://gitlab.example.com/api/v4/projects/7/pipelines\">\n[{\"id\":41,\"status\":\"failed\"}]\n</browser_response>",
            result);
        Assert.Contains("\\u0022GET\\u0022, \\u0022/api/v4/projects/7/pipelines\\u0022, null,", _gitlab.DevTools[0].Parameters, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"result":{"type":"object","value":{"ok":false,"error":"other site"}}}""", "только к сайту самой вкладки")]
    [InlineData("""{"result":{"type":"object","value":{"ok":false,"error":"Failed to fetch"}}}""", "Failed to fetch")]
    public async Task Request_ThatCannotGo_IsExplained(string reply, string expected)
    {
        _gitlab.DevToolsReplies["Runtime.evaluate"] = reply;

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "request", ["tab"] = "t1", ["url"] = "https://evil.example/x" }));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownMethod_IsExplained()
    {
        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "request", ["tab"] = "t1", ["url"] = "/x", ["method"] = "TRACE" }));

        Assert.Contains("TRACE", error.Message, StringComparison.Ordinal);
        Assert.Contains("PATCH", error.Message, StringComparison.Ordinal);
    }

    // Reading the API of a site the agent may open is like reading its pages; a change always asks.
    [Fact]
    public async Task Reading_AnAllowedSiteIsFree_AChangeAlwaysAsks()
    {
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("GET", "/api/v4/projects")));
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call(null, "/api/v4/projects")));
        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("POST", "/api/v4/projects/7/pipeline")));
        Assert.True(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, Call("POST", "/api/v4/projects/7/pipeline")));
        Assert.False(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, Call("GET", "/api/v4/projects")));

        var card = Assert.Single(await _tools.PreviewAsync(BrowserAgentTools.ToolName, Call("post", "/api/v4/projects/7/pipeline", """{"ref":"main"}"""), TestContext.Current.CancellationToken));
        Assert.Equal(
            ("Агент хочет отправить запрос на сайт под вашим входом", "gitlab.example.com", "POST /api/v4/projects/7/pipeline\n{\"ref\":\"main\"}"),
            (card.Title, card.Header, card.NewText));
    }

    [Fact]
    public void Reading_AnUnknownSiteAsks_AndOffersToAllowIt_ThisMachineNeverAsks()
    {
        _gitlab.Load("https://argocd.example.com/applications", "Argo CD");

        Assert.False(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("GET", "/api/v1/applications")));
        Assert.Equal("argocd.example.com", _approvals.SuggestRule(BrowserAgentTools.ToolName, Call("GET", "/api/v1/applications")));
        Assert.Null(_approvals.SuggestRule(BrowserAgentTools.ToolName, Call("POST", "/api/v1/applications/shop/sync")));

        _gitlab.Load("http://localhost:5000/", "Приложение");
        Assert.True(_approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("DELETE", "/api/orders/15")));
        Assert.False(_approvals.AlwaysAsks(BrowserAgentTools.ToolName, Call("DELETE", "/api/orders/15")));
    }

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments) =>
        (await _tools.CreateTools().OfType<AIFunction>().Single().InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;

    private static Dictionary<string, object?> Call(string? method, string url, string? body = null) =>
        new() { ["action"] = "request", ["tab"] = "t1", ["method"] = method, ["url"] = url, ["body"] = body };

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
