using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Panels;

/// <summary>
/// Browser tabs on real WebView2 (ADR 0054): a second tab loads its own page while the first keeps its one, a link
/// "in a new window" opens a tab and closing it returns to the opener, Ctrl+T and Ctrl+W act on browser tabs while the
/// panel has focus, and tabs come back after a restart.
/// </summary>
public sealed class BrowserTabsTests : IDisposable
{
    private const string ShowBrowserCommand = "workbench.view.browser";

    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly LocalSite _site = LocalSite.Start(new Dictionary<string, string>
    {
        ["/"] = "<html><head><title>Первая</title></head><body><h1>Первая страница</h1><a href=\"/second\" target=\"_blank\">Открыть вторую</a></body></html>",
        ["/second"] = "<html><head><title>Вторая</title></head><body><h1>Вторая страница</h1></body></html>",
        ["/third"] = "<html><head><title>Третья</title></head><body><h1>Третья страница</h1></body></html>",
    });

    public void Dispose()
    {
        _site.Dispose();
        AppSession.DeleteQuietly(_userData);
    }

    [Fact]
    public void Tabs_OpenSwitchClose_LinkInNewWindowIsATab_AndTheyComeBackAfterRestart()
    {
        using (var session = AppSession.WithUserData(_userData))
        {
            ShowBrowser(session);
            Open(session, "/");
            WaitForPage(session, "Первая страница");
            WaitForTab(session, "Browser.Tab.t1", "Первая");

            // A second tab loads its own page; the first keeps its one.
            session.Find("Browser.NewTab").GuardedClick();
            session.WaitFor("Browser.Tab.t2");
            Open(session, "/third");
            WaitForPage(session, "Третья страница");
            WaitForTab(session, "Browser.Tab.t2", "Третья");

            session.Find("Browser.Tab.t1").GuardedClick();
            WaitForAddress(session, "/");
            WaitForPage(session, "Первая страница");

            // A link "in a new window" opens a tab next to its opener; closing it returns there.
            Retry.WhileNull(() => session.MainWindow.FindFirstDescendant(condition => condition.ByName("Открыть вторую")), PageTimeout).Result!.GuardedClick();
            WaitForTab(session, "Browser.Tab.t3", "Вторая");
            WaitForAddress(session, "/second");
            session.SaveScreenshot("browser-tabs");
            session.Find("Browser.Tab.t3.Close").GuardedClick();
            session.WaitUntilGone("Browser.Tab.t3");
            WaitForAddress(session, "/");

            // With focus in the panel, Ctrl+T and Ctrl+W act on browser tabs, not on the editor tab.
            session.Find("Browser.Address").GuardedClick();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
            session.WaitFor("Browser.Tab.t4");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            session.WaitUntilGone("Browser.Tab.t4");
            session.WaitFor("Browser.Address");
        }

        using var restarted = AppSession.WithUserData(_userData);
        if (restarted.TryFind("Browser.Address") is null)
        {
            ShowBrowser(restarted);
        }

        // The tab shown last is shown again and loads; the other one loads when it is first shown.
        WaitForTab(restarted, "Browser.Tab.t1", "Первая");
        WaitForTab(restarted, "Browser.Tab.t2", "Третья");
        WaitForPage(restarted, "Третья страница");
        restarted.Find("Browser.Tab.t1").GuardedClick();
        WaitForPage(restarted, "Первая страница");
        WaitForAddress(restarted, "/");
    }

    private static void ShowBrowser(AppSession session)
    {
        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem(ShowBrowserCommand)).GuardedClick();
        session.WaitFor("Browser.Address");
    }

    private void Open(AppSession session, string path)
    {
        session.TypeInto("Browser.Address", _site.Address(path));
        Keyboard.Type(VirtualKeyShort.ENTER);
    }

    private void WaitForAddress(AppSession session, string path) =>
        session.WaitForValue("Browser.Address", value => value == $"http://localhost:{_site.Port}{path}", PageTimeout);

    private static void WaitForPage(AppSession session, string text)
    {
        if (Retry.WhileNull(() => session.MainWindow.FindFirstDescendant(condition => condition.ByName(text)), PageTimeout).Success)
        {
            return;
        }

        var shot = session.SaveScreenshot("browser-tabs-failure");
        Assert.Fail($"Текст страницы «{text}» не появился в окне. Адрес: {session.ValueOf("Browser.Address")}; ошибка: {session.TryFind("Browser.Error")?.Name}; снимок: {shot}; журнал: {AppSession.ReadLog(session.UserDataFolder)}");
    }

    private static void WaitForTab(AppSession session, string automationId, string title) =>
        Assert.True(Retry.WhileFalse(() => session.TryFind(automationId)?.Name == title, PageTimeout).Success,
            $"Вкладка {automationId} не получила заголовок «{title}».");
}
