using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Browser panel and approvals (ADR 0027): an address without a scheme is completed, state follows the browser; pages
/// on this machine open at once, other sites through a card; console events become text for the model; feed rows.
/// </summary>
public sealed class BrowserPanelTests
{
    [Theory]
    [InlineData("localhost:5000/login", "http://localhost:5000/login")]
    [InlineData("learn.microsoft.com/dotnet", "https://learn.microsoft.com/dotnet")]
    [InlineData("http://127.0.0.1:8080", "http://127.0.0.1:8080/")]
    public void Address_WithoutScheme_IsCompleted(string address, string expected) =>
        Assert.Equal(new Uri(expected), BrowserViewModel.Normalize(address));

    [Fact]
    public async Task Go_OpensTheAddress_StateFollowsTheBrowser()
    {
        var browser = new FakeBrowserEngine();
        using var panel = new BrowserViewModel(browser) { Address = "localhost:5000" };

        await panel.GoCommand.ExecuteAsync(null);

        Assert.Equal([new Uri("http://localhost:5000")], browser.Navigations);
        Assert.Equal(("http://localhost:5000/", "Страница"), (panel.Address, panel.Title));
    }

    // A page opened by another module (a container port) goes into the address bar.
    [Fact]
    public async Task Open_PutsTheAddressIntoTheBar_AndNavigates()
    {
        var browser = new FakeBrowserEngine();
        using var panel = new BrowserViewModel(browser);

        await panel.OpenAsync(new Uri("http://localhost:8080"));

        Assert.Equal([new Uri("http://localhost:8080")], browser.Navigations);
        Assert.Equal("http://localhost:8080/", panel.Address);
    }

    [Fact]
    public async Task BrowserFailure_IsShownInThePanel()
    {
        var browser = new FakeBrowserEngine { Failure = new BrowserException("Браузер недоступен: нет WebView2") };
        using var panel = new BrowserViewModel(browser) { Address = "localhost:5000" };

        await panel.GoCommand.ExecuteAsync(null);

        Assert.Equal("Браузер недоступен: нет WebView2", panel.Error);
    }

    [Theory]
    [InlineData("http://localhost:5000", true)]
    [InlineData("http://app.localhost/", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("https://example.org", false)]
    public void Navigate_LocalAtOnce_OtherSitesAsk(string url, bool preapproved) =>
        Assert.Equal(preapproved, Approvals(out _).IsPreapproved(BrowserAgentTools.ToolName, Call("navigate", url)));

    [Fact]
    public void PageActions_AtOnce_AllowAlwaysAddsTheSite()
    {
        var approvals = Approvals(out var settings);

        Assert.True(approvals.IsPreapproved(BrowserAgentTools.ToolName, Call("click", null)));
        Assert.Null(approvals.SuggestRule(BrowserAgentTools.ToolName, Call("navigate", "http://localhost:5000")));
        Assert.Equal("example.org", approvals.SuggestRule(BrowserAgentTools.ToolName, Call("navigate", "https://example.org/x")));
        approvals.AllowAlways(BrowserAgentTools.ToolName, "example.org");
        Assert.Equal(new[] { "example.org" }, settings.Written[BrowserApprovals.AllowedHostsKey]);
    }

    [Fact]
    public void ConsoleEvents_BecomeText()
    {
        Assert.Equal(new BrowserConsoleMessage("error", "Failed 404"), BrowserConsoleEvents.Parse(BrowserConsoleEvents.ConsoleApiCalled,
            """{"type":"error","args":[{"type":"string","value":"Failed"},{"type":"number","value":404}]}"""));
        Assert.Equal(new BrowserConsoleMessage("exception", "TypeError: x is undefined\n    at main.js:3"), BrowserConsoleEvents.Parse(BrowserConsoleEvents.ExceptionThrown,
            """{"exceptionDetails":{"text":"Uncaught","exception":{"description":"TypeError: x is undefined\n    at main.js:3"}}}"""));
        Assert.Equal(new BrowserConsoleMessage("error", "Failed to load resource (http://localhost:5000/app.js)"), BrowserConsoleEvents.Parse(BrowserConsoleEvents.LogEntryAdded,
            """{"entry":{"level":"error","text":"Failed to load resource","url":"http://localhost:5000/app.js"}}"""));
    }

    [Fact]
    public void Feed_NamesTheAction()
    {
        var presenter = new BrowserToolPresenter();

        var navigate = presenter.Present(new AgentToolCall(BrowserAgentTools.ToolName, Call("navigate", "http://localhost:5000/login/")))!;
        var click = presenter.Present(new AgentToolCall(BrowserAgentTools.ToolName, new Dictionary<string, object?> { ["action"] = "click", ["ref"] = "e3" }))!;

        Assert.Equal((AgentToolIcon.Web, "Браузер: localhost:5000/login", false), (navigate.Icon, navigate.Title, navigate.IsExploration));
        Assert.Equal("Браузер: щелчок e3", click.Title);
        Assert.Null(presenter.Present(new AgentToolCall("read_file", new Dictionary<string, object?>())));
    }

    private static BrowserApprovals Approvals(out FakeSettingsService settings)
    {
        settings = new FakeSettingsService();
        return new BrowserApprovals(new TestOptionsMonitor<BrowserOptions>(new BrowserOptions()), settings, NullLogger<BrowserApprovals>.Instance);
    }

    private static Dictionary<string, object?> Call(string action, string? url) => new() { ["action"] = action, ["url"] = url };
}
