using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// The <c>browser</c> tool (ADR 0027) over a fake browser: navigate, click and type return a fresh snapshot, model refs
/// enter scripts as JSON strings, browser and page errors are explained to the model, and a screenshot is an image in
/// the next message (ADR 0030).
/// </summary>
public sealed class BrowserAgentToolsTests
{
    private readonly FakeBrowserEngine _browser = new();
    private readonly FakeAgentImages _images = new();
    private readonly AIFunction _tool;

    public BrowserAgentToolsTests() =>
        _tool = new BrowserAgentTools(_browser, new PassThroughOutputStore(), _images).CreateTools().OfType<AIFunction>().Single();

    [Fact]
    public async Task Navigate_OpensThePage_AndReturnsTheSnapshot()
    {
        var result = await InvokeAsync(new() { ["action"] = "navigate", ["url"] = "http://localhost:5000/login" });

        Assert.Equal([new Uri("http://localhost:5000/login")], _browser.Navigations);
        Assert.StartsWith("<browser_page url=\"http://localhost:5000/login\" title=\"Вход\">\n- heading \"Вход\"", result, StringComparison.Ordinal);
        Assert.EndsWith("</browser_page>", result, StringComparison.Ordinal);
    }

    // The model's ref and text go in as JSON strings: a quote in the text cannot break or extend the script.
    [Fact]
    public async Task Type_PutsArgumentsAsJson_WaitsForLoad_ThenSnapshot()
    {
        await InvokeAsync(new() { ["action"] = "type", ["ref"] = "e1", ["text"] = "a\"); alert(1); (\"", ["submit"] = true });

        var script = _browser.Scripts[0];
        Assert.Contains("(\"e1\", \"a\\u0022); alert(1); (\\u0022\", true)", script, StringComparison.Ordinal);
        Assert.Equal(1, _browser.Waits);
        Assert.Equal(BrowserScripts.Snapshot, _browser.Scripts[^1]);
    }

    [Fact]
    public async Task Click_UnknownRef_AsksForANewSnapshot()
    {
        _browser.ActionResult = """{"ok":false,"error":"not found"}""";

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "click", ["ref"] = "e99" }));

        Assert.Contains("новый снимок", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Console_ListsMessagesSinceTheLastCall()
    {
        _browser.Console.Add(new BrowserConsoleMessage("error", "TypeError: x is undefined"));

        Assert.Equal("[error] TypeError: x is undefined", await InvokeAsync(new() { ["action"] = "console" }));
        Assert.Contains("нет", await InvokeAsync(new() { ["action"] = "console" }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("navigate", "file:///C:/secret.txt", "http")]
    [InlineData("scroll", null, "snapshot")]
    [InlineData("click", null, "ref")]
    public async Task BadCalls_AreExplained(string action, string? url, string expected)
    {
        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = action, ["url"] = url }));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    // When the browser fails to open a page, the model learns why and the step does not crash.
    [Fact]
    public async Task BrowserFailure_GoesToTheModel()
    {
        _browser.Failure = new BrowserException("Страница не открылась: ConnectionAborted");

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "navigate", ["url"] = "http://localhost:5000" }));

        Assert.Equal("Страница не открылась: ConnectionAborted", error.Message);
    }

    [Fact]
    public async Task Screenshot_GoesToTheModel_AsPng()
    {
        await InvokeAsync(new() { ["action"] = "navigate", ["url"] = "http://localhost:5000/login" });

        var result = await InvokeAsync(new() { ["action"] = "screenshot" });

        Assert.Equal("Снимок экрана http://localhost:5000/login — в следующем сообщении.", result);
        var (name, data, mediaType) = Assert.Single(_images.Shown);
        Assert.Equal(("http://localhost:5000/login", "image/png"), (name, mediaType));
        Assert.Equal(_browser.Screenshots[BrowserImageFormat.Png], data);
    }

    // A PNG over the model's limit is resent as JPEG.
    [Fact]
    public async Task Screenshot_TooLargePng_IsSentAsJpeg()
    {
        _images.MaxBytes = 2;

        await InvokeAsync(new() { ["action"] = "screenshot" });

        Assert.Equal([BrowserImageFormat.Png, BrowserImageFormat.Jpeg], _browser.Captures);
        Assert.Equal("image/jpeg", Assert.Single(_images.Shown).MediaType);
    }

    [Fact]
    public async Task Screenshot_ForAModelWithoutVision_SaysToUseTheSnapshot()
    {
        _images.CanShow = false;

        var result = await InvokeAsync(new() { ["action"] = "screenshot" });

        Assert.Contains("snapshot", result, StringComparison.Ordinal);
        Assert.Empty(_browser.Captures);
    }

    [Fact]
    public async Task Preview_OfExternalSite_NamesIt()
    {
        var preview = Assert.Single(await new BrowserAgentTools(_browser, new PassThroughOutputStore(), _images)
            .PreviewAsync(BrowserAgentTools.ToolName, new Dictionary<string, object?> { ["action"] = "navigate", ["url"] = "https://example.org/a" }, TestContext.Current.CancellationToken));

        Assert.Equal(("Агент хочет открыть страницу в браузере", "example.org", "GET https://example.org/a"), (preview.Title, preview.Header, preview.NewText));
    }

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;

    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }

    /// <summary>Images for the model: records shown ones; the test sets the size limit.</summary>
    private sealed class FakeAgentImages : IAgentImages
    {
        public bool CanShow { get; set; } = true;

        public long MaxBytes { get; set; } = long.MaxValue;

        public List<(string Name, byte[] Data, string MediaType)> Shown { get; } = [];

        public bool TryShow(string name, byte[] data, string mediaType)
        {
            if (data.Length > MaxBytes)
            {
                return false;
            }

            Shown.Add((name, data, mediaType));
            return true;
        }
    }
}
