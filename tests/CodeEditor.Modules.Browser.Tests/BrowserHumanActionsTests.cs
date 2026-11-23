using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Acting like a person (ADR 0054): a click is a real mouse click with a script click as the fallback, hover and drag
/// move the mouse, a file goes into a file field without the system dialog and only from the folder, and a dialog the
/// page opens is answered by the agent.
/// </summary>
public sealed class BrowserHumanActionsTests
{
    private static readonly string Project = Path.GetFullPath(@"C:\work\shop");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(Project);
    private readonly BrowserAgentTabs _tabs;
    private readonly FakeBrowserPage _browser;
    private readonly BrowserAgentTools _tools;
    private readonly AIFunction _tool;

    public BrowserHumanActionsTests()
    {
        var workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        workspace.Open(Project);
        _tabs = FakeBrowserEngine.AgentTabs(out _browser);
        _browser.Load("https://shop.example.com/orders", "Заказы");
        _tools = new BrowserAgentTools(_tabs, new PassThroughOutputStore(), new NoImages(), workspace, _fileSystem);
        _tool = _tools.CreateTools().OfType<AIFunction>().Single();
    }

    [Fact]
    public async Task Click_IsARealMouseClick_AtTheElement()
    {
        await InvokeAsync(new() { ["action"] = "click", ["ref"] = "e3" });

        Assert.Equal(
            [
                """{"type":"mouseMoved","x":120,"y":40,"button":"none","buttons":0,"clickCount":0}""",
                """{"type":"mousePressed","x":120,"y":40,"button":"left","buttons":1,"clickCount":1}""",
                """{"type":"mouseReleased","x":120,"y":40,"button":"left","buttons":0,"clickCount":1}""",
            ],
            _browser.DevTools.Where(call => call.Method == BrowserPointer.Method).Select(call => call.Parameters));
        Assert.DoesNotContain(BrowserScripts.Click("e3"), _browser.Scripts);
        Assert.Equal(1, _browser.Waits);
    }

    // Something covers the element, or the page is behind another tab and takes no input: the click still happens.
    [Fact]
    public async Task Click_ThatDidNotReachTheElement_FallsBackToAScriptClick()
    {
        _browser.ClickReached = false;

        await InvokeAsync(new() { ["action"] = "click", ["ref"] = "e3" });

        Assert.Contains(BrowserScripts.Click("e3"), _browser.Scripts);
    }

    // A real click on a native list or a file field would open a system window nobody can answer.
    [Fact]
    public async Task Click_OnAListIsAScriptClick_OnAFileFieldPointsToUpload()
    {
        _browser.PointKind = "select";
        _browser.ClickReached = false;
        await InvokeAsync(new() { ["action"] = "click", ["ref"] = "e3" });
        Assert.Empty(_browser.DevTools);

        _browser.PointKind = "file";
        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "click", ["ref"] = "e4" }));
        Assert.Contains("upload", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hover_MovesTheMouse_DragHoldsTheButtonOnTheWay()
    {
        await InvokeAsync(new() { ["action"] = "hover", ["ref"] = "e3" });
        Assert.Equal("""{"type":"mouseMoved","x":120,"y":40,"button":"none","buttons":0,"clickCount":0}""", Assert.Single(_browser.DevTools).Parameters);

        _browser.DevTools.Clear();
        await InvokeAsync(new() { ["action"] = "drag", ["ref"] = "e3", ["to"] = "e9" });
        var events = _browser.DevTools.Select(call => call.Parameters).ToList();
        Assert.Equal(BrowserPointer.DragSteps + 3, events.Count);
        Assert.Contains("mousePressed", events[1], StringComparison.Ordinal);
        Assert.All(events[2..^1], moved => Assert.Contains("\"type\":\"mouseMoved\",", moved, StringComparison.Ordinal));
        Assert.All(events[2..^1], moved => Assert.Contains("\"buttons\":1", moved, StringComparison.Ordinal));
        Assert.Contains("mouseReleased", events[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Drag_OfADraggableElement_UsesThePagesDragAndDrop()
    {
        _browser.IsDraggable = true;

        await InvokeAsync(new() { ["action"] = "drag", ["ref"] = "e3", ["to"] = "e9" });

        Assert.Contains(BrowserScripts.DragDrop("e3", "e9"), _browser.Scripts);
        Assert.Empty(_browser.DevTools);
    }

    [Fact]
    public async Task Upload_PutsAFolderFileIntoTheField_ThroughDevTools()
    {
        _fileSystem.AddFile(Path.Combine(Project, "data", "orders.csv"), "id;sum");
        _browser.DevToolsReplies["Runtime.evaluate"] = """{"result":{"type":"object","subtype":"node","objectId":"obj-7"}}""";

        var result = await InvokeAsync(new() { ["action"] = "upload", ["ref"] = "e5", ["path"] = "data/orders.csv" });

        Assert.StartsWith("Файл data/orders.csv выбран в поле.\n<browser_page", result, StringComparison.Ordinal);
        var set = _browser.DevTools.Single(call => call.Method == "DOM.setFileInputFiles").Parameters;
        Assert.Contains("\"objectId\":\"obj-7\"", set, StringComparison.Ordinal);
        Assert.Contains("orders.csv", set, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../other/secret.txt", "вне рабочей папки")]
    [InlineData(".env", ".env")]
    [InlineData("missing.csv", "missing.csv")]
    public async Task Upload_OnlyAFileOfTheFolder_NeverASecret(string path, string expected)
    {
        _fileSystem.AddFile(Path.Combine(Project, ".env"), "KEY=1");

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "upload", ["ref"] = "e5", ["path"] = path }));

        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_browser.DevTools);
    }

    // A file put into a form leaves the machine: a card names the file and the field, except on a page of this machine.
    [Fact]
    public async Task Upload_ToASiteNeedsACard()
    {
        var approvals = new BrowserApprovals(new TestOptionsMonitor<BrowserOptions>(new BrowserOptions()), _tabs, new FakeSettingsService(), NullLogger<BrowserApprovals>.Instance);
        var call = new Dictionary<string, object?> { ["action"] = "upload", ["ref"] = "e5", ["path"] = "data/orders.csv" };
        _browser.ScriptReplies[BrowserScripts.Describe("e5")] = new Queue<string>(["""{"ok":true,"role":"input","name":"Файл заказов"}"""]);

        Assert.False(approvals.IsPreapproved(BrowserAgentTools.ToolName, call));
        var card = Assert.Single(await _tools.PreviewAsync(BrowserAgentTools.ToolName, call, TestContext.Current.CancellationToken));
        Assert.Equal(("Агент хочет отправить файл на сайт", "shop.example.com", "data/orders.csv → «Файл заказов» (input)"), (card.Title, card.Header, card.NewText));

        _browser.Load("http://localhost:5000/import", "Импорт");
        Assert.True(approvals.IsPreapproved(BrowserAgentTools.ToolName, call));
    }

    // The page opened a confirm: the call says so at once, and the agent answers it.
    [Fact]
    public async Task Dialog_IsReportedInsteadOfATimeout_AndAnsweredByTheAgent()
    {
        _browser.Dialog = new BrowserDialog("confirm", "Удалить заказ 15?", string.Empty);

        var blocked = await InvokeAsync(new() { ["action"] = "snapshot" });
        var answered = await InvokeAsync(new() { ["action"] = "dialog", ["accept"] = false });

        Assert.Equal("Страница показала диалог и ждёт ответа: confirm «Удалить заказ 15?». Ответь действием dialog (accept=true или false; text — для prompt); до ответа страница не реагирует.", blocked);
        Assert.Equal((false, null), Assert.Single(_browser.DialogAnswers));
        Assert.StartsWith("<browser_page", answered, StringComparison.Ordinal);
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
