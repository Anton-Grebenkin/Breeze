using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// The browser tool beyond clicks: real key presses, waiting for text, scripts, the page's requests and responses, the
/// HTTP status of an error page, and which of these need approval.
/// </summary>
public sealed class BrowserPageActionsTests
{
    private readonly FakeBrowserEngine _browser = new();
    private readonly BrowserNetworkLog _network = new();
    private readonly AIFunction _tool;

    public BrowserPageActionsTests() =>
        _tool = new BrowserAgentTools(_browser, new BrowserPageActions(_browser, _network), new PassThroughOutputStore(), new NoImages())
            .CreateTools().OfType<AIFunction>().Single();

    [Fact]
    public async Task Press_FocusesTheElement_SendsARealKeyPress_ThenSnapshot()
    {
        var result = await InvokeAsync(new() { ["action"] = "press", ["key"] = "enter", ["ref"] = "e1" });

        Assert.Equal(BrowserScripts.Focus("e1"), _browser.Scripts[0]);
        Assert.Equal(["Input.dispatchKeyEvent", "Input.dispatchKeyEvent"], _browser.DevTools.Select(call => call.Method));
        Assert.Contains("\"type\":\"keyDown\"", _browser.DevTools[0].Parameters, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"keyUp\"", _browser.DevTools[1].Parameters, StringComparison.Ordinal);
        Assert.StartsWith("<browser_page", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Press_UnknownKey_ListsTheKeys()
    {
        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "press", ["key"] = "F13" }));

        Assert.Contains("ArrowDown", error.Message, StringComparison.Ordinal);
        Assert.Empty(_browser.DevTools);
    }

    [Fact]
    public async Task WaitFor_TextAppears_ReturnsTheSnapshot()
    {
        _browser.ScriptReplies[BrowserScripts.HasText("Готово")] = new(["false", "true"]);

        var result = await InvokeAsync(new() { ["action"] = "wait_for", ["text"] = "Готово" });

        Assert.StartsWith("<browser_page", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitFor_TextNeverAppears_SaysSo_WithThePageAsItIs()
    {
        _browser.ActionResult = "false";

        var result = await InvokeAsync(new() { ["action"] = "wait_for", ["text"] = "Готово", ["timeoutSeconds"] = 1 });

        Assert.StartsWith("Текст «Готово» не появился за 1 с.", result, StringComparison.Ordinal);
        Assert.Contains("<browser_page", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Evaluate_ReturnsTheValueAsJson_OrTheError()
    {
        _browser.DevToolsReplies["Runtime.evaluate"] = """{"result":{"type":"object","value":{"user":"admin","roles":2}}}""";
        var value = await InvokeAsync(new() { ["action"] = "evaluate", ["script"] = "fetch('/api/me').then(r => r.json())" });
        _browser.DevToolsReplies["Runtime.evaluate"] = """{"result":{"type":"object"},"exceptionDetails":{"text":"Uncaught","exception":{"description":"ReferenceError: x is not defined"}}}""";
        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "evaluate", ["script"] = "x" }));

        Assert.Equal("""{"user":"admin","roles":2}""", value);
        Assert.Contains("\"awaitPromise\":true", _browser.DevTools[0].Parameters, StringComparison.Ordinal);
        Assert.Contains("ReferenceError: x is not defined", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Network_ListsApiCalls_ResponseReadsTheBody()
    {
        _network.Record(BrowserNetworkLog.RequestWillBeSent, """{"requestId":"7","type":"XHR","request":{"url":"http://localhost:5000/api/items","method":"GET"}}""");
        _network.Record(BrowserNetworkLog.ResponseReceived, """{"requestId":"7","response":{"status":404,"mimeType":"application/json"}}""");
        _network.Record(BrowserNetworkLog.RequestWillBeSent, """{"requestId":"8","type":"Image","request":{"url":"http://localhost:5000/logo.png","method":"GET"}}""");
        _network.Record(BrowserNetworkLog.ResponseReceived, """{"requestId":"8","response":{"status":200,"mimeType":"image/png"}}""");
        _browser.DevToolsReplies["Network.getResponseBody"] = """{"body":"eyJlcnJvciI6Im5vdCBmb3VuZCJ9","base64Encoded":true}""";

        var list = await InvokeAsync(new() { ["action"] = "network" });
        var body = await InvokeAsync(new() { ["action"] = "response", ["id"] = "7" });

        Assert.Equal("[7] GET 404 XHR http://localhost:5000/api/items", list);
        Assert.Equal("[7] GET 404 XHR http://localhost:5000/api/items application/json\n{\"error\":\"not found\"}", body);
        Assert.Equal(("Network.getResponseBody", """{"requestId":"7"}"""), _browser.DevTools[^1]);
    }

    [Fact]
    public void NetworkLog_KeepsFailures_SkipsDataUrls()
    {
        _network.Record(BrowserNetworkLog.RequestWillBeSent, """{"requestId":"2","type":"Fetch","request":{"url":"http://localhost:5001/graphql","method":"POST"}}""");
        _network.Record(BrowserNetworkLog.LoadingFailed, """{"requestId":"2","errorText":"net::ERR_CONNECTION_REFUSED"}""");
        _network.Record(BrowserNetworkLog.RequestWillBeSent, """{"requestId":"3","type":"Image","request":{"url":"data:image/png;base64,AAAA","method":"GET"}}""");

        Assert.Equal(["[2] POST net::ERR_CONNECTION_REFUSED Fetch http://localhost:5001/graphql"], _network.Recent(10).Select(BrowserNetworkLog.Describe));
    }

    // A server's error page loads like any page: the model reads it, with its status.
    [Fact]
    public async Task ErrorPage_SnapshotCarriesTheStatus()
    {
        _browser.StatusCode = 404;

        var result = await InvokeAsync(new() { ["action"] = "navigate", ["url"] = "http://localhost:5000/missing" });

        Assert.StartsWith("<browser_page url=\"http://localhost:5000/login\" title=\"Вход\" status=\"404\">", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CannotConnect", true)]
    [InlineData("Unknown", true)]
    [InlineData("CertificateExpired", false)]
    public void NavigationErrors_SayWhenTheServerDoesNotAnswer(string status, bool hint) =>
        Assert.Equal(hint, BrowserErrors.Describe(status).Contains("Приложение запущено?", StringComparison.Ordinal));

    // A script on a site where the user is logged in could send the session elsewhere: it asks, with the script shown.
    [Theory]
    [InlineData("http://localhost:5000/", true)]
    [InlineData("https://github.com/", false)]
    public async Task Evaluate_AsksOnSitesOfOtherMachines(string page, bool preapproved)
    {
        await _browser.NavigateAsync(new Uri(page), TestContext.Current.CancellationToken);
        var approvals = new BrowserApprovals(new TestOptionsMonitor<BrowserOptions>(new BrowserOptions()), _browser, new FakeSettingsService(), NullLogger<BrowserApprovals>.Instance);
        var call = new Dictionary<string, object?> { ["action"] = "evaluate", ["script"] = "document.cookie" };

        var preview = Assert.Single(await new BrowserAgentTools(_browser, new BrowserPageActions(_browser, _network), new PassThroughOutputStore(), new NoImages())
            .PreviewAsync(BrowserAgentTools.ToolName, call, TestContext.Current.CancellationToken));

        Assert.Equal(preapproved, approvals.IsPreapproved(BrowserAgentTools.ToolName, call));
        Assert.Equal("document.cookie", preview.NewText);
        Assert.Null(approvals.SuggestRule(BrowserAgentTools.ToolName, call));
    }

    [Fact]
    public void Feed_NamesTheNewActions()
    {
        var presenter = new BrowserToolPresenter();

        AgentToolView Present(Dictionary<string, object?> arguments) => presenter.Present(new AgentToolCall(BrowserAgentTools.ToolName, arguments))!;

        Assert.Equal("Браузер: клавиша Enter", Present(new() { ["action"] = "press", ["key"] = "Enter" }).Title);
        Assert.Equal("Браузер: ожидание «Готово»", Present(new() { ["action"] = "wait_for", ["text"] = "Готово" }).Title);
        Assert.True(Present(new() { ["action"] = "network" }).IsExploration);
    }

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;

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
