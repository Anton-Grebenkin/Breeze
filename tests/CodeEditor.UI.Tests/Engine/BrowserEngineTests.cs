using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Shell.Services;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using Microsoft.Extensions.AI;

namespace CodeEditor.UI.Tests.Engine;

/// <summary>
/// The product's browser engine on real WebView2 pages, without a visible window or input (ADR 0054): a dialog in the
/// agent's tab is reported at once and answered by the agent, a window a page opens becomes a tab the agent continues
/// in, and a tab kept from the last run shows its address at once and loads when first called.
/// </summary>
public sealed class BrowserEngineTests : IDisposable
{
    private readonly OffScreenBrowser _browser = new();
    private readonly LocalSite _site = LocalSite.Start(new Dictionary<string, string>
    {
        ["/"] = """
            <html><head><title>Заказы</title></head><body><h1>Список заказов</h1>
            <button onclick="if (confirm('Удалить заказ?')) document.title = 'Удалён'">Удалить</button>
            <button onclick="document.title = 'Имя: ' + prompt('Как назвать?', 'без имени')">Назвать</button>
            <a href="/report" target="_blank">Отчёт</a>
            </body></html>
            """,
        ["/report"] = "<html><head><title>Отчёт</title></head><body><h1>Отчёт за месяц</h1></body></html>",
        ["/secure"] = LocalSite.SignInRequired,
        ["/words"] = """
            <html><head><title>Слова</title></head><body><h1>Слова</h1><p>альфа бета альфа</p><p>Альфа</p>
            <button id="go" onclick="document.title = 'нажато'">Кнопка выбора</button>
            </body></html>
            """,
    });

    private readonly BrowserTabs _tabs;
    private readonly AIFunction _tool;

    public BrowserEngineTests()
    {
        _tabs = new BrowserTabs(_browser.Engine);
        _tool = new BrowserAgentTools(new BrowserAgentTabs(_tabs), new PassThroughOutputStore(), new NoImages()).CreateTools().OfType<AIFunction>().Single();
    }

    public void Dispose()
    {
        _tabs.Dispose();
        _browser.Dispose();
        _site.Dispose();
    }

    [Fact]
    public async Task Dialog_InTheAgentsTab_IsReportedAtOnce_AndAnswered()
    {
        var page = await UseAsync(new() { ["action"] = "navigate", ["url"] = Address("/") });
        Assert.Contains("- button \"Удалить\" [ref=e1]", page, StringComparison.Ordinal);

        var started = DateTime.UtcNow;
        var blocked = await UseAsync(new() { ["action"] = "click", ["ref"] = "e1" });
        Assert.Equal("Страница показала диалог и ждёт ответа: confirm «Удалить заказ?». Ответь действием dialog (accept=true или false; text — для prompt); до ответа страница не реагирует.", blocked);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10), "Диалог должен вернуть управление сразу, а не по истечении ожидания.");

        // Until it is answered every call says the same.
        Assert.Equal(blocked, await UseAsync(new() { ["action"] = "snapshot" }));

        var dismissed = await UseAsync(new() { ["action"] = "dialog", ["accept"] = false });
        Assert.Contains("title=\"Заказы\"", dismissed, StringComparison.Ordinal);

        await UseAsync(new() { ["action"] = "click", ["ref"] = "e1" });
        var accepted = await UseAsync(new() { ["action"] = "dialog" });
        Assert.Contains("title=\"Удалён\"", accepted, StringComparison.Ordinal);

        await UseAsync(new() { ["action"] = "click", ["ref"] = "e2" });
        var named = await UseAsync(new() { ["action"] = "dialog", ["text"] = "срочный" });
        Assert.Contains("title=\"Имя: срочный\"", named, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LinkInANewWindow_BecomesATab_TheAgentContinuesIn()
    {
        await UseAsync(new() { ["action"] = "navigate", ["url"] = Address("/") });

        var result = await UseAsync(new() { ["action"] = "click", ["ref"] = "e3" });

        Assert.StartsWith("Действие открыло новую вкладку t2; ты продолжаешь в ней.\n<browser_page tab=\"t2\"", result, StringComparison.Ordinal);
        var report = await UseAsync(new() { ["action"] = "wait_for", ["text"] = "Отчёт за месяц" });
        Assert.Contains("- heading \"Отчёт за месяц\"", report, StringComparison.Ordinal);
        Assert.Equal(["t1", "t2"], _tabs.Tabs.Select(tab => tab.Id));
        Assert.Equal("t1", _tabs.Tabs[1].OpenerId);
    }

    // The tabs share one profile, as in an ordinary browser: what the user signed in to in their tab — cookies and the
    // site's stored data — is there in the agent's tab. Only the per-tab session storage is each tab's own.
    [Fact]
    public async Task SignInInTheUsersTab_IsThereInTheAgentsTab()
    {
        var mine = _tabs.Open();
        await mine.Page.NavigateAsync(new Uri(Address("/")), TestContext.Current.CancellationToken);
        await mine.Page.EvaluateAsync(
            "document.cookie = 'session=abc123; path=/'; localStorage.setItem('token', 'xyz'); sessionStorage.setItem('draft', 'mine')", TestContext.Current.CancellationToken);

        await UseAsync(new() { ["action"] = "navigate", ["url"] = Address("/report") });
        var seen = await UseAsync(new() { ["action"] = "evaluate", ["script"] = "document.cookie + '|' + localStorage.getItem('token') + '|' + sessionStorage.getItem('draft')" });

        Assert.Equal("\"session=abc123|xyz|null\"", seen);
        Assert.Equal((2, false, true), (_tabs.Tabs.Length, _tabs.Tabs[0].IsAgent, _tabs.Tabs[1].IsAgent));
    }

    // A site behind the browser's own sign-in window, opened in the agent's background tab: the agent is told at once
    // instead of waiting for the load to time out, and the tab is shown to the user.
    [Fact]
    public async Task SignInWindowOfTheBrowser_IsReportedAtOnce_AndTheTabIsShownToTheUser()
    {
        var notifications = new Notifications();
        using var attention = new BrowserAttention(_tabs, notifications);
        var mine = _tabs.Open();
        await mine.Page.NavigateAsync(new Uri(Address("/")), TestContext.Current.CancellationToken);

        var started = DateTime.UtcNow;
        var error = await Assert.ThrowsAsync<AgentToolException>(() => UseAsync(new() { ["action"] = "navigate", ["url"] = Address("/secure") }));

        Assert.Contains("сайт просит логин и пароль в окне самого браузера", error.Message, StringComparison.Ordinal);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(15), "Окно входа должно вернуть управление сразу, а не по истечении ожидания загрузки.");
        Assert.True(_tabs.Active!.IsAgent);
        Assert.Contains("просит логин и пароль", Assert.Single(notifications.Shown), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindOnPage_CountsTheMatches_AndWalksThem()
    {
        await UseAsync(new() { ["action"] = "navigate", ["url"] = Address("/words") });
        var page = _tabs.Tabs[0].Page;

        var first = await page.FindAsync("альфа", BrowserFindStep.Start, TestContext.Current.CancellationToken);
        var second = await page.FindAsync("альфа", BrowserFindStep.Next, TestContext.Current.CancellationToken);
        var none = await page.FindAsync("гамма", BrowserFindStep.Start, TestContext.Current.CancellationToken);
        var stopped = await page.FindAsync(string.Empty, BrowserFindStep.Start, TestContext.Current.CancellationToken);

        Assert.Equal((new BrowserFindResult(1, 3), new BrowserFindResult(2, 3), new BrowserFindResult(0, 0), default(BrowserFindResult)), (first, second, none, stopped));
    }

    // Picking an element: the click goes to the picker, not to the page, and a message the page forges is ignored.
    [Fact]
    public async Task PickElement_ReturnsWhatWasClicked_ThePageDoesNotGetTheClick_AndCannotForgeAPick()
    {
        await UseAsync(new() { ["action"] = "navigate", ["url"] = Address("/words") });
        var page = _tabs.Tabs[0].Page;

        var pick = page.PickElementAsync(TestContext.Current.CancellationToken);
        await page.EvaluateAsync("window.chrome.webview.postMessage({ breezePick: 'forged', tag: 'x', text: '', selector: '', html: '' })", TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        Assert.False(pick.IsCompleted);

        await PageMouse.PressAsync(page, "e1", TestContext.Current.CancellationToken);

        var picked = await pick.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(("button", "Кнопка выбора", "#go"), (picked!.Tag, picked.Text, picked.Selector));
        Assert.StartsWith("<button id=\"go\"", picked.Html, StringComparison.Ordinal);
        Assert.Equal("\"Слова\"", await page.EvaluateAsync("document.title", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestoredPage_ShowsItsAddressAtOnce_AndLoadsWhenFirstCalled()
    {
        var page = _browser.Engine.CreatePage(new BrowserPageState(new Uri(Address("/report")), "Отчёт (сохранённый)"));
        Assert.Equal((Address("/report"), "Отчёт (сохранённый)", false), (page.Url!.AbsoluteUri, page.Title, page.IsLoading));

        var text = await page.EvaluateAsync("document.querySelector('h1').innerText", TestContext.Current.CancellationToken);

        Assert.Equal("\"Отчёт за месяц\"", text);
        Assert.True(Retry.WhileFalse(() => page.Title == "Отчёт", TimeSpan.FromSeconds(10)).Result, page.Title);
        page.Dispose();
    }

    private string Address(string path) => $"http://localhost:{_site.Port}{path}";

    private async Task<string> UseAsync(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;

    private sealed class Notifications : INotificationService
    {
        public List<string> Shown { get; } = [];

        public IDisposable Show(Notification notification)
        {
            Shown.Add(notification.Message);
            return new Closed();
        }

        private sealed class Closed : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

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
