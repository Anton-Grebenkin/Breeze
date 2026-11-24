using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Browser tabs (ADR 0054): several pages, one shown; a window a page opens becomes a tab next to it; the user's tabs
/// come back after a restart, per folder, and load only when shown; the strip follows the tabs.
/// </summary>
public sealed class BrowserTabsTests
{
    private static readonly string UserData = Path.GetFullPath(@"C:\user");
    private static readonly string Project = Path.GetFullPath(@"C:\work\shop");
    private static readonly string Other = Path.GetFullPath(@"C:\work\blog");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(UserData).AddDirectory(Project).AddDirectory(Other);
    private readonly FakeBrowserEngine _engine = new();
    private readonly Workspace _workspace;

    public BrowserTabsTests() => _workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);

    [Fact]
    public void NewTab_IsShown_ClosingItShowsTheNeighbour_AndClosesThePage()
    {
        using var tabs = new BrowserTabs(_engine);
        var first = tabs.Open();
        var second = tabs.Open();
        var third = tabs.Open(select: false);

        Assert.Equal(["t1", "t2", "t3"], tabs.Tabs.Select(tab => tab.Id));
        Assert.Same(second, tabs.Active);

        tabs.Close(second);

        Assert.Same(third, tabs.Active);
        Assert.True(((FakeBrowserPage)second.Page).IsDisposed);
        tabs.Close(third);
        Assert.Same(first, tabs.Active);
        tabs.Close(first);
        Assert.Null(tabs.Active);
    }

    // A link "in a new window" or a sign-in popup: the window is a tab next to its opener, and closing it returns there.
    [Fact]
    public void WindowOpenedByAPage_BecomesATabNextToIt_ClosingReturnsToTheOpener()
    {
        using var tabs = new BrowserTabs(_engine);
        var opener = tabs.Open();
        var last = tabs.Open(select: false);

        var popup = ((FakeBrowserPage)opener.Page).OpenWindow("https://login.example.com/")!;

        Assert.Equal([opener.Page, popup, last.Page], tabs.Tabs.Select(tab => tab.Page));
        var tab = tabs.Active!;
        Assert.Equal((popup, opener.Id, BrowserTabOwner.User), (tab.Page, tab.OpenerId, tab.Owner));

        tabs.Select(last);
        popup.CloseWindow();

        Assert.Equal(2, tabs.Tabs.Length);
        Assert.Same(last, tabs.Active);
        Assert.True(popup.IsDisposed);
    }

    // A window opened from a background tab of the agent stays in the background and is the agent's too.
    [Fact]
    public void WindowOpenedInABackgroundAgentTab_StaysInTheBackground()
    {
        using var tabs = new BrowserTabs(_engine);
        var mine = tabs.Open();
        var agent = tabs.Open(BrowserTabOwner.Agent, select: false);

        ((FakeBrowserPage)agent.Page).OpenWindow();

        Assert.Same(mine, tabs.Active);
        Assert.True(tabs.Tabs[^1].IsAgent);
    }

    [Fact]
    public void Pinned_GoFirst_AndSurviveCloseOthers_NextAndPreviousGoAround()
    {
        using var tabs = new BrowserTabs(_engine);
        var (first, second, third) = (tabs.Open(), tabs.Open(), tabs.Open());

        tabs.SetPinned(third, true);
        tabs.SetPinned(second, true);

        Assert.Equal([third, second, first], tabs.Tabs);
        tabs.SetPinned(third, false);
        Assert.Equal([second, third, first], tabs.Tabs);

        tabs.Select(first);
        tabs.SelectNext(1);
        Assert.Same(second, tabs.Active);
        tabs.SelectNext(-1);
        Assert.Same(first, tabs.Active);

        tabs.CloseOthers(first);
        Assert.Equal([second, first], tabs.Tabs);
    }

    [Fact]
    public async Task UserTabs_ComeBackAfterRestart_PerFolder_AgentTabsDoNot()
    {
        _workspace.Open(Project);
        using var store = Store();
        using (var tabs = new BrowserTabs(_engine, store))
        {
            ((FakeBrowserPage)tabs.Open().Page).Load("https://gitlab.example.com/shop/-/pipelines?page=2", "Pipelines");
            var grafana = tabs.Open();
            ((FakeBrowserPage)grafana.Page).Load("https://grafana.example.com/d/abc", "Kafka");
            tabs.SetPinned(grafana, true);
            ((FakeBrowserPage)tabs.Open(BrowserTabOwner.Agent, select: false).Page).Load("https://example.org/", "Example");
            tabs.Open(select: false);
            await store.Saved;
        }

        using var reopened = new BrowserTabs(new FakeBrowserEngine(), store);

        Assert.Equal(
            [("https://grafana.example.com/d/abc", "Kafka", true), ("https://gitlab.example.com/shop/-/pipelines?page=2", "Pipelines", false)],
            reopened.Tabs.Select(tab => (tab.Page.Url!.AbsoluteUri, tab.Page.Title, tab.IsPinned)));
        Assert.Equal("Kafka", reopened.Active!.Page.Title);

        // Another folder has its own tabs; coming back restores these.
        _workspace.Open(Other);
        Assert.Empty(reopened.Tabs);
        _workspace.Open(Project);
        Assert.Equal(2, reopened.Tabs.Length);
    }

    [Fact]
    public void Strip_FollowsTheTabs_AddressBarShowsTheShownPage()
    {
        using var tabs = new BrowserTabs(_engine);
        using var panel = new BrowserViewModel(tabs, new InlineUiDispatcher());
        var first = tabs.Open();
        ((FakeBrowserPage)first.Page).Load("http://localhost:5000/orders", "Заказы");
        var agent = tabs.Open(BrowserTabOwner.Agent, select: false);
        ((FakeBrowserPage)agent.Page).Load("https://example.org/docs", string.Empty);

        Assert.Equal([("Заказы", true, false), ("example.org/docs", false, true)], panel.Tabs.Select(tab => (tab.Title, tab.IsActive, tab.IsAgent)));
        Assert.Equal(("http://localhost:5000/orders", "Заказы"), (panel.Address, panel.Title));

        panel.Tabs[1].SelectCommand.Execute(null);
        Assert.Equal("https://example.org/docs", panel.Address);

        panel.NewTabCommand.Execute(null);
        Assert.Equal((3, "Новая вкладка", string.Empty), (panel.Tabs.Count, panel.ActiveTab!.Title, panel.Address));

        panel.CloseTabCommand.Execute(null);
        panel.Tabs[0].TogglePinCommand.Execute(null);
        Assert.Equal([true, false], panel.Tabs.Select(tab => tab.IsPinned));
    }

    // Typing in the address bar must survive the page changing its title or finishing a load.
    [Fact]
    public void TypedAddress_SurvivesATitleChangeOfThePage()
    {
        using var tabs = new BrowserTabs(_engine);
        using var panel = new BrowserViewModel(tabs, new InlineUiDispatcher());
        var page = (FakeBrowserPage)tabs.Open().Page;
        page.Load("http://localhost:5000/", "Главная");

        panel.Address = "localhost:5000/ord";
        page.Load("http://localhost:5000/", "Главная (1)");

        Assert.Equal(("localhost:5000/ord", "Главная (1)"), (panel.Address, panel.Title));
    }

    [Fact]
    public async Task PageFromAnotherModule_OpensInANewTab_UnlessTheShownOneIsEmpty()
    {
        using var tabs = new BrowserTabs(_engine);
        using var panel = new BrowserViewModel(tabs, new InlineUiDispatcher());

        await panel.OpenAsync(new Uri("http://localhost:8080/"));
        await panel.OpenAsync(new Uri("http://localhost:9090/"));
        panel.NewTabCommand.Execute(null);
        await panel.OpenAsync(new Uri("http://localhost:7070/"));

        Assert.Equal(["http://localhost:8080/", "http://localhost:9090/", "http://localhost:7070/"], tabs.Tabs.Select(tab => tab.Page.Url!.AbsoluteUri));
    }

    private BrowserTabStore Store() => new(_fileSystem, new UserDataPaths(UserData), _workspace, NullLogger<BrowserTabStore>.Instance);
}
