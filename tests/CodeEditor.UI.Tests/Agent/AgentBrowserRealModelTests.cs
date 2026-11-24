using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// The agent and browser tabs on a real model, end to end in the real app. It spends tokens, so it runs only when
/// <c>BREEZE_REAL_MODEL</c> names the model (for example <c>deepseek/deepseek-v4.1-flash</c>); the service is the
/// editor's default one and the key is the user's own, copied encrypted from their editor data. The task is what a
/// person would ask: a stand is open in the user's tab; find the app that is out of sync, open it in the agent's own
/// tab, press Sync and confirm. The chat and the model traffic are kept next to the screenshots for a look afterwards.
/// </summary>
public sealed class AgentBrowserRealModelTests : IDisposable
{
    public const string ModelVariable = "BREEZE_REAL_MODEL";

    private const string ShowBrowserCommand = "workbench.view.browser";
    private const string Request =
        "В браузере у меня открыт стенд с приложениями. Найди приложение, которое рассинхронизировано, открой его страницу в своей вкладке, " +
        "нажми Sync и подтверди. Напиши, какая версия развернулась и какой проверочный код показала страница.";

    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromMinutes(5);

    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
    private readonly LocalSite _site = LocalSite.Start(new Dictionary<string, string>
    {
        ["/apps"] = """
            <html><head><title>Стенд: приложения</title></head><body>
            <h1>Приложения стенда</h1>
            <table>
              <tr><th>Приложение</th><th>Состояние</th><th>Версия</th><th></th></tr>
              <tr><td>blog</td><td>Synced</td><td>1.8.0</td><td><a href="/apps/blog">Открыть</a></td></tr>
              <tr><td>shop</td><td>OutOfSync</td><td>2.4.0</td><td><a href="/apps/shop">Открыть</a></td></tr>
              <tr><td>billing</td><td>Synced</td><td>5.1.3</td><td><a href="/apps/billing">Открыть</a></td></tr>
            </table>
            </body></html>
            """,
        ["/apps/shop"] = """
            <html><head><title>shop — стенд</title></head><body>
            <h1>shop</h1>
            <p>Состояние: <b id="state">OutOfSync</b></p>
            <p>Развёрнутая версия: <span id="version">2.4.0</span>; в репозитории: 2.4.1</p>
            <button onclick="if (confirm('Синхронизировать shop с репозиторием?')) { document.getElementById('state').textContent = 'Synced'; document.getElementById('version').textContent = '2.4.1'; document.getElementById('log').textContent = 'Синхронизация завершена, проверочный код: ' + (7000 + 391); }">Sync</button>
            <button onclick="alert('Удаление на стенде запрещено')">Delete</button>
            <p id="log"></p>
            </body></html>
            """,
    });

    public void Dispose()
    {
        _site.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void RealModel_ReadsTheUsersTab_WorksInItsOwn_PressesSyncAndConfirms()
    {
        var model = Environment.GetEnvironmentVariable(ModelVariable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(model), $"Прогон на настоящей модели тратит токены: задайте {ModelVariable}.");
        Arrange(model!);
        var output = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "real-model")).FullName;

        using (var session = AppSession.WithUserData(_userData, _folder))
        {
            session.WaitFor("Explorer.Node.A.cs");
            session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
            session.WaitFor(AutomationIds.MenuItem(ShowBrowserCommand)).GuardedClick();
            session.TypeInto("Browser.Address", _site.Address("/apps"));
            Keyboard.Type(VirtualKeyShort.ENTER);
            WaitForTab(session, "Browser.Tab.t1", "Стенд: приложения");

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
            session.WaitForFocus("Agent.Input");
            session.TypeInto("Agent.Input", Request);
            Keyboard.Type(VirtualKeyShort.ENTER);

            // The turn runs while the Stop button is shown.
            session.WaitFor("Agent.Stop");
            Assert.True(Retry.WhileTrue(() => session.TryFind("Agent.Stop") is not null, TurnTimeout).Success, "Ход не закончился за отведённое время.");
            session.SaveScreenshot("real-model-user-tab");
            var address = session.ValueOf("Browser.Address");
            if (session.TryFind("Browser.Tab.t2") is { } agentTab)
            {
                agentTab.GuardedClick();
                session.WaitForValue("Browser.Address", value => value != address, TimeSpan.FromSeconds(5));
                session.SaveScreenshot("real-model-agent-tab");
            }

            Keep(output);
            Assert.Equal($"http://localhost:{_site.Port}/apps", address);
        }

        var chat = string.Concat(Directory.EnumerateFiles(output, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("7391", chat, StringComparison.Ordinal);
        Assert.Contains("2.4.1", chat, StringComparison.Ordinal);
    }

    // The model from the variable on the editor's default service; the key is the user's own file, still encrypted for
    // this Windows account. Traffic is logged so the run can be studied.
    private void Arrange(string model)
    {
        File.WriteAllText(Path.Combine(_folder, "A.cs"), "class A { }\r\n");
        File.WriteAllText(Path.Combine(_userData, "settings.json"), $$"""{ "agent.model": "{{model}}", "agent.autoMemory": false, "agent.trafficLog": true }""");
        var secrets = new UserDataPaths(root: null).File("secrets");
        Assert.True(Directory.Exists(secrets), $"В данных редактора нет ключа сервиса моделей: {secrets}");
        var target = Directory.CreateDirectory(Path.Combine(_userData, "secrets")).FullName;
        foreach (var file in Directory.EnumerateFiles(secrets))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
    }

    // The chat of the run and the log with the model traffic, for a look after the app's folders are gone.
    private void Keep(string output)
    {
        foreach (var (from, to) in (ReadOnlySpan<(string, string)>)[(Path.Combine(_folder, ".breeze", "agent"), "chat"), (Path.Combine(_userData, "logs"), "logs")])
        {
            if (!Directory.Exists(from))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var copy = Path.Combine(output, to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(file, copy, overwrite: true);
            }
        }
    }

    private static void WaitForTab(AppSession session, string automationId, string title) =>
        Assert.True(Retry.WhileFalse(() => session.TryFind(automationId)?.Name == title, PageTimeout).Success,
            $"Вкладка {automationId} не получила заголовок «{title}».");
}
