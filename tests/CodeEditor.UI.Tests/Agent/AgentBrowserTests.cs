using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// The agent and browser tabs on real WebView2 and a fake model (ADR 0054): the agent is told which tabs are open,
/// opens its page in a tab of its own behind the user's one, and reads the user's tab by id without touching it.
/// </summary>
public sealed class AgentBrowserTests : IDisposable
{
    private const string ShowBrowserCommand = "workbench.view.browser";

    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

    private readonly FakeOpenAIServer _model = new();
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;
    private readonly LocalSite _site = LocalSite.Start(new Dictionary<string, string>
    {
        ["/mine"] = "<html><head><title>Моя</title></head><body><h1>Моя страница</h1></body></html>",
        ["/orders"] = "<html><head><title>Заказы</title></head><body><h1>Список заказов</h1></body></html>",
    });

    public AgentBrowserTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(_folder, "A.cs"), "class A { }\r\n");

        // The agent talks to a local fake model; the key is a test key that never leaves this server.
        File.WriteAllText(Path.Combine(_userData, "settings.json"), $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false }""");
        new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(_userData)).Set("agent-api-key", "test-key");
    }

    public void Dispose()
    {
        _model.Dispose();
        _site.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void Agent_SeesTheTabs_WorksInItsOwn_AndReadsTheUsers()
    {
        _model
            .CallTool("browser", new { action = "navigate", url = $"http://localhost:{_site.Port}/orders" })
            .CallTool("browser", new { action = "snapshot", tab = "t1" })
            .Reply("Готово.");

        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem(ShowBrowserCommand)).GuardedClick();
        session.TypeInto("Browser.Address", _site.Address("/mine"));
        Keyboard.Type(VirtualKeyShort.ENTER);
        WaitForTab(session, "Browser.Tab.t1", "Моя");

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        Assert.Equal("localhost", session.WaitFor("Agent.Context.BrowserTabs").Name);
        session.TypeInto("Agent.Input", "Проверь заказы");
        Keyboard.Type(VirtualKeyShort.ENTER);

        Assert.True(Retry.WhileFalse(() => _model.Requests.Count == 3, PageTimeout).Result, $"Запросов к модели: {_model.Requests.Count}");
        var requests = _model.Requests.ToArray();
        Assert.Contains($"t1: localhost:{_site.Port}/mine (активная)", requests[0], StringComparison.Ordinal);
        Assert.Contains("Список заказов", requests[1], StringComparison.Ordinal);
        Assert.Contains("Моя страница", requests[2], StringComparison.Ordinal);

        // The agent's page is a tab of its own behind the user's one, which stays where it was.
        WaitForTab(session, "Browser.Tab.t2", "Заказы");
        Assert.Equal($"http://localhost:{_site.Port}/mine", session.ValueOf("Browser.Address"));
        session.SaveScreenshot("browser-agent-tab");
    }

    private static void WaitForTab(AppSession session, string automationId, string title) =>
        Assert.True(Retry.WhileFalse(() => session.TryFind(automationId)?.Name == title, PageTimeout).Success,
            $"Вкладка {automationId} не получила заголовок «{title}».");
}
