using CodeEditor.Core.Commands;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// What a person does with the shown page (ADR 0054): find on page, zoom, the browser's own windows, the system
/// browser, and sending the page, the selection or a picked element to the agent's chat.
/// </summary>
public sealed class BrowserPageToolsTests
{
    private readonly BrowserTabs _tabs;
    private readonly FakeBrowserPage _page;
    private readonly RecordingCommands _commands = new();
    private readonly FakeSystemShell _shell = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly BrowserPageToolsViewModel _tools;

    public BrowserPageToolsTests()
    {
        _tabs = FakeBrowserEngine.Tabs(out _page);
        _page.Load("https://gitlab.example.com/shop/-/pipelines?page=2", "Pipelines");
        _tools = new BrowserPageToolsViewModel(_tabs, _commands, _shell, _statusBar);
    }

    [Fact]
    public async Task Find_SearchesAsTheTextChanges_WalksTheMatches_ClosingClearsTheHighlight()
    {
        var focused = 0;
        _tools.FindFocusRequested += (_, _) => focused++;

        await _tools.ShowFindCommand.ExecuteAsync(null);
        _tools.FindText = "failed";
        await _tools.FindNextCommand.ExecuteAsync(null);
        await _tools.FindPreviousCommand.ExecuteAsync(null);

        Assert.Equal((true, 1, "1 из 3"), (_tools.IsFindOpen, focused, _tools.FindStatus));
        Assert.Equal([("failed", BrowserFindStep.Start), ("failed", BrowserFindStep.Next), ("failed", BrowserFindStep.Previous)], _page.Finds[1..]);

        _page.FindCount = 0;
        _tools.FindText = "passed";
        Assert.Equal("нет совпадений", _tools.FindStatus);

        await _tools.CloseFindCommand.ExecuteAsync(null);
        Assert.Equal((false, string.Empty, (string.Empty, BrowserFindStep.Start)), (_tools.IsFindOpen, _tools.FindStatus, _page.Finds[^1]));
    }

    [Fact]
    public async Task Zoom_StepsThroughBrowserLevels_AndResets()
    {
        await _tools.ZoomInCommand.ExecuteAsync(null);
        await _tools.ZoomInCommand.ExecuteAsync(null);
        Assert.Equal(1.25, _page.Zoom);

        await _tools.ZoomOutCommand.ExecuteAsync(null);
        await _tools.ZoomOutCommand.ExecuteAsync(null);
        await _tools.ZoomOutCommand.ExecuteAsync(null);
        Assert.Equal(0.9, _page.Zoom);

        await _tools.ZoomResetCommand.ExecuteAsync(null);
        Assert.Equal(1, _page.Zoom);
    }

    [Fact]
    public async Task BrowserWindows_AndTheSystemBrowser_OpenForTheShownPage()
    {
        await _tools.DevToolsCommand.ExecuteAsync(null);
        await _tools.DownloadsCommand.ExecuteAsync(null);
        await _tools.OpenExternalCommand.ExecuteAsync(null);

        Assert.Equal([BrowserWindowKind.DevTools, BrowserWindowKind.Downloads], _page.Opened);
        Assert.Equal("https://gitlab.example.com/shop/-/pipelines?page=2", _shell.Opened!.AbsoluteUri);
    }

    [Fact]
    public async Task Page_GoesToTheChatInput_AsItsTabAndAddress()
    {
        await _tools.SendPageCommand.ExecuteAsync(null);

        Assert.Equal((AgentChatCommands.AddToInput, "Страница в браузере, вкладка t1: https://gitlab.example.com/shop/-/pipelines?page=2"), Assert.Single(_commands.Executed));
    }

    [Fact]
    public async Task Selection_GoesToTheChatAsAQuote_NothingSelectedIsSaid()
    {
        _page.ScriptReplies["window.getSelection().toString()"] = new Queue<string>(["\"job build failed\\nexit code 1\"", "\"\""]);

        await _tools.SendSelectionCommand.ExecuteAsync(null);
        await _tools.SendSelectionCommand.ExecuteAsync(null);

        var (_, text) = Assert.Single(_commands.Executed);
        Assert.Equal("Выделено на странице https://gitlab.example.com/shop/-/pipelines?page=2 (вкладка t1):\n> job build failed\n> exit code 1", text);
        Assert.Equal("На странице ничего не выделено.", _statusBar.Message);
    }

    [Fact]
    public async Task PickedElement_GoesToTheChat_WithItsSelectorAndMarkup_EscapeSendsNothing()
    {
        _page.Picked = new BrowserPickedElement("button", "Retry", "#jobs > li:nth-of-type(2) > button", "<button class=\"btn\">Retry</button>");

        await _tools.PickElementCommand.ExecuteAsync(null);

        var (_, text) = Assert.Single(_commands.Executed);
        Assert.Equal(
            "Элемент на странице https://gitlab.example.com/shop/-/pipelines?page=2 (вкладка t1): <button> «Retry»\nСелектор: #jobs > li:nth-of-type(2) > button\n```html\n<button class=\"btn\">Retry</button>\n```",
            text);
        Assert.False(_tools.IsPicking);

        _page.Picked = null;
        await _tools.PickElementCommand.ExecuteAsync(null);
        Assert.Single(_commands.Executed);
    }

    // A page cannot send a pick of its own: only the answer that carries the pending token counts.
    [Fact]
    public void PickAnswer_WithoutThePendingToken_IsIgnored()
    {
        const string Answer = """{"breezePick":"abc","tag":"a","text":"Docs","selector":"nav > a","html":"<a>Docs</a>"}""";

        Assert.Equal(new BrowserPickedElement("a", "Docs", "nav > a", "<a>Docs</a>"), BrowserPicker.Parse(Answer, "abc", out var cancelled));
        Assert.False(cancelled);
        Assert.Null(BrowserPicker.Parse(Answer, "other", out _));
        Assert.Null(BrowserPicker.Parse("""{"tag":"a"}""", "abc", out _));
        Assert.Null(BrowserPicker.Parse("not json", "abc", out _));
        Assert.Null(BrowserPicker.Parse("""{"breezePick":"abc","cancelled":true}""", "abc", out cancelled));
        Assert.True(cancelled);
    }

    // An empty tab has no page to act on: nothing happens and nothing fails.
    [Fact]
    public async Task EmptyTab_DoesNothing()
    {
        _tabs.Open();

        await _tools.SendPageCommand.ExecuteAsync(null);
        await _tools.ZoomInCommand.ExecuteAsync(null);
        await _tools.OpenExternalCommand.ExecuteAsync(null);

        Assert.Empty(_commands.Executed);
        Assert.Null(_shell.Opened);
    }

    private sealed class RecordingCommands : ICommandService
    {
        public List<(string Id, string? Argument)> Executed { get; } = [];

        public bool CanExecute(string commandId) => true;

        public ValueTask<CommandExecutionStatus> ExecuteAsync(string commandId, object? argument = null, CancellationToken cancellationToken = default)
        {
            Executed.Add((commandId, argument as string));
            return ValueTask.FromResult(default(CommandExecutionStatus));
        }
    }
}
