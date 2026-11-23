using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// A page in the agent's background tab shows the browser's own sign-in window (ADR 0054): nobody would see it there,
/// so the tab is shown to the user with a notification — once per window, and only for the agent's tabs.
/// </summary>
public sealed class BrowserAttentionTests : IDisposable
{
    private readonly BrowserTabs _tabs = new(new FakeBrowserEngine());
    private readonly FakeNotifications _notifications = new();
    private readonly BrowserAttention _attention;
    private readonly BrowserTab _mine;
    private int _panelShown;

    public BrowserAttentionTests()
    {
        _attention = new BrowserAttention(_tabs, _notifications, () =>
        {
            _panelShown++;
            return Task.CompletedTask;
        });
        _mine = _tabs.Open();
        ((FakeBrowserPage)_mine.Page).Load("https://gitlab.example.com/", "GitLab");
    }

    public void Dispose() => _attention.Dispose();

    [Fact]
    public void SignInWindowInTheAgentsTab_ShowsTheTabToTheUser_Once()
    {
        var agent = _tabs.Open(BrowserTabOwner.Agent, select: false);
        var page = (FakeBrowserPage)agent.Page;
        page.Load("https://wiki.corp.example/start", string.Empty);

        page.ShowSignInWindow();
        page.ShowSignInWindow();

        Assert.Same(agent, _tabs.Active);
        Assert.Equal(1, _panelShown);
        Assert.Equal("Сайт wiki.corp.example во вкладке агента просит логин и пароль. Введите их в браузере: агент ждёт.", Assert.Single(_notifications.Shown).Message);

        // The window is gone (signed in or dismissed); the next one is told about again.
        page.ShowSignInWindow(shown: false);
        _tabs.Select(_mine);
        page.ShowSignInWindow();
        Assert.Same(agent, _tabs.Active);
        Assert.Equal(2, _notifications.Shown.Count);
    }

    // In a tab of the user's the window is already in front of them.
    [Fact]
    public void SignInWindowInTheUsersTab_NeedsNoNotice()
    {
        var other = _tabs.Open(select: false);

        ((FakeBrowserPage)other.Page).ShowSignInWindow();

        Assert.Same(_mine, _tabs.Active);
        Assert.Empty(_notifications.Shown);
        Assert.Equal(0, _panelShown);
    }
}
