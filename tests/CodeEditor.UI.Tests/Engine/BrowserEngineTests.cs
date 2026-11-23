using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
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
