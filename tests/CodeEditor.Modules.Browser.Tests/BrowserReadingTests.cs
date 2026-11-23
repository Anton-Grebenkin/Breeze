using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Reading;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Reading a page (ADR 0054): a long snapshot comes in parts cut at line ends, the main text comes as Markdown, "find"
/// names the places, and a screenshot can show one element.
/// </summary>
public sealed class BrowserReadingTests
{
    private readonly BrowserAgentTabs _tabs;
    private readonly FakeBrowserPage _browser;
    private readonly Images _images = new();
    private readonly AIFunction _tool;

    public BrowserReadingTests()
    {
        _tabs = FakeBrowserEngine.AgentTabs(out _browser);
        _tool = new BrowserAgentTools(_tabs, new PassThroughOutputStore(), _images).CreateTools().OfType<AIFunction>().Single();
    }

    [Fact]
    public void Parts_AreCutAtLineEnds_AndALongLineByCharacters()
    {
        var lines = string.Join('\n', Enumerable.Range(1, 3000).Select(index => $"- text \"строка {index}\""));

        var (first, number, count) = PageParts.Take(lines, part: null);
        var (last, lastNumber, _) = PageParts.Take(lines, part: 99);

        Assert.Equal((1, true), (number, count > 3));
        Assert.True(first.Length <= PageParts.Size);
        Assert.StartsWith("- text \"строка 1\"", first, StringComparison.Ordinal);
        Assert.EndsWith("\"", first, StringComparison.Ordinal);
        Assert.Equal(count, lastNumber);
        Assert.EndsWith("- text \"строка 3000\"", last, StringComparison.Ordinal);
        Assert.Equal(lines, string.Join('\n', Enumerable.Range(1, count).Select(part => PageParts.Take(lines, part).Text)));

        var single = new string('x', PageParts.Size * 2 + 5);
        Assert.Equal((PageParts.Size, 3), (PageParts.Take(single, 1).Text.Length, PageParts.Take(single, 1).Count));
        Assert.Equal(("короткий", 1, 1), PageParts.Take("короткий", part: 5));
    }

    [Fact]
    public async Task LongSnapshot_ComesInParts_TheNoteSaysHowToGetTheNext()
    {
        var text = string.Join('\n', Enumerable.Range(1, 1500).Select(index => $"- text \"строка {index}\""));
        _browser.SnapshotResult = JsonSerializer.Serialize(new { title = "Журнал", url = "http://localhost:5000/log", text, truncated = false });

        var first = await InvokeAsync(new() { ["action"] = "snapshot" });
        var second = await InvokeAsync(new() { ["action"] = "snapshot", ["part"] = 2 });

        Assert.StartsWith("<browser_page tab=\"t1\" url=\"http://localhost:5000/log\" title=\"Журнал\" part=\"1/3\">\n- text \"строка 1\"", first, StringComparison.Ordinal);
        Assert.EndsWith("…(часть 1 из 3; следующая: snapshot с part=2)\n</browser_page>", first, StringComparison.Ordinal);
        Assert.Contains("part=\"2/3\"", second, StringComparison.Ordinal);
        Assert.DoesNotContain("\"строка 1\"\n", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_ReturnsTheMainTextAsMarkdown()
    {
        var result = await InvokeAsync(new() { ["action"] = "read" });

        Assert.Equal("<browser_text tab=\"t1\" url=\"http://localhost:5000/login\" title=\"Вход\">\n# Вход\n\nВведите почту.\n</browser_text>", result);
    }

    [Fact]
    public async Task Find_NamesThePlaces_WithHeadingRefAndCount()
    {
        _browser.ScriptReplies[BrowserReading.Find("Sync")] = new Queue<string>(
        [
            """{"total":3,"places":[{"heading":"Applications","count":2,"context":"…shop Sync status: Synced…","ref":"e12"},{"heading":"","count":1,"context":"Last Sync: today","ref":null}]}""",
            """{"total":0,"places":[]}""",
        ]);

        var found = await InvokeAsync(new() { ["action"] = "find", ["text"] = "Sync" });
        var missing = await InvokeAsync(new() { ["action"] = "find", ["text"] = "Sync" });

        Assert.Equal(
            [
                "«Sync»: совпадений 3, мест 2.",
                "1. под заголовком «Applications» [ref=e12] ×2: …shop Sync status: Synced…",
                "2.: Last Sync: today",
            ],
            found.Split('\n'));
        Assert.Equal("Текста «Sync» на странице нет.", missing);
    }

    // The model's text is a JSON string inside the script: a quote cannot end it.
    [Fact]
    public void Find_PutsTheTextAsJson()
    {
        Assert.EndsWith("})(\"a\\u0022); alert(1); (\\u0022\")", BrowserReading.Find("a\"); alert(1); (\""), StringComparison.Ordinal);
    }

    // A chart is looked at alone: the screenshot is clipped to the element's box.
    [Fact]
    public async Task Screenshot_OfAnElement_IsClippedToItsBox()
    {
        _browser.ScriptReplies[BrowserScripts.Box("e4")] = new Queue<string>(["""{"ok":true,"x":40,"y":900,"width":600,"height":320}"""]);
        _browser.DevToolsReplies["Page.captureScreenshot"] = """{"data":"AQID"}""";

        await InvokeAsync(new() { ["action"] = "screenshot", ["ref"] = "e4" });

        var (method, parameters) = Assert.Single(_browser.DevTools);
        Assert.Equal("Page.captureScreenshot", method);
        Assert.Equal("""{"format":"png","clip":{"x":40,"y":900,"width":600,"height":320,"scale":1},"captureBeyondViewport":true}""", parameters);
        Assert.Equal([1, 2, 3], Assert.Single(_images.Shown));
        Assert.Empty(_browser.Captures);
    }

    [Fact]
    public async Task Screenshot_OfAMissingElement_AsksForANewSnapshot()
    {
        _browser.ScriptReplies[BrowserScripts.Box("e9")] = new Queue<string>(["""{"ok":false,"error":"not found"}"""]);

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "screenshot", ["ref"] = "e9" }));

        Assert.Contains("новый снимок", error.Message, StringComparison.Ordinal);
    }

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;

    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }

    private sealed class Images : IAgentImages
    {
        public bool CanShow => true;

        public List<byte[]> Shown { get; } = [];

        public bool TryShow(string name, byte[] data, string mediaType)
        {
            Shown.Add(data);
            return true;
        }
    }
}
