using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Modules.Browser.Services.Web;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Search through the browser (ADR 0054): the engine from the setting opens in a background tab of the agent's, its
/// results come back as title, address and excerpt; a page that asks for a person is shown to the user and waited on.
/// </summary>
public sealed class BrowserWebAccessTests
{
    private const string TwoHits = """
        {"hits":[{"title":"Channel<T> — .NET","url":"https://learn.microsoft.com/dotnet/channels","snippet":"Каналы для обмена данными."},
                 {"title":"Blog","url":"https://devblogs.microsoft.com/channels","snippet":""}],"needsUser":false}
        """;

    private const string Captcha = """{"hits":[],"needsUser":true}""";

    private readonly FakeBrowserEngine _engine = new();
    private readonly BrowserTabs _tabs;
    private readonly FakeNotifications _notifications = new();
    private readonly TestOptionsMonitor<BrowserOptions> _options = new(new BrowserOptions());
    private readonly BrowserWebAccess _web;
    private int _panelShown;

    public BrowserWebAccessTests()
    {
        _tabs = new BrowserTabs(_engine);
        ((FakeBrowserPage)_tabs.Open().Page).Load("https://gitlab.example.com/", "GitLab");
        _web = new BrowserWebAccess(_tabs, _options, _notifications, () =>
        {
            _panelShown++;
            return Task.CompletedTask;
        })
        {
            Delay = (_, _) => Task.CompletedTask,
        };
    }

    [Fact]
    public async Task Search_OpensTheEngineInABackgroundAgentTab_AndReadsItsResults()
    {
        Reply(TwoHits, TwoHits);

        var hits = await _web.SearchAsync("c# channel пример", TestContext.Current.CancellationToken);
        await _web.SearchAsync("второй запрос", TestContext.Current.CancellationToken);

        Assert.Equal(
            [("Channel<T> — .NET", "https://learn.microsoft.com/dotnet/channels", "Каналы для обмена данными."), ("Blog", "https://devblogs.microsoft.com/channels", string.Empty)],
            hits.Select(hit => (hit.Title, hit.Url, hit.Snippet)));
        var search = Assert.Single(_tabs.Tabs, tab => tab.IsAgent);
        Assert.Equal("https://html.duckduckgo.com/html/?q=c%23%20channel%20%D0%BF%D1%80%D0%B8%D0%BC%D0%B5%D1%80", SearchPage.Navigations[0].AbsoluteUri);
        Assert.Equal(2, SearchPage.Navigations.Count);
        Assert.NotSame(search, _tabs.Active);
        Assert.Equal(0, _panelShown);
    }

    [Theory]
    [InlineData("yandex", "https://yandex.ru/search/?text=kafka%20lag")]
    [InlineData("https://search.corp.example/find?q={query}&lang=ru", "https://search.corp.example/find?q=kafka%20lag&lang=ru")]
    public void Engine_IsAKnownName_OrOnesOwnAddress(string engine, string expected) =>
        Assert.Equal(expected, BrowserSearchEngines.Url(engine, "kafka lag").AbsoluteUri);

    [Fact]
    public async Task UnknownEngine_IsExplained()
    {
        _options.Set(new BrowserOptions { SearchEngine = "altavista" });

        var error = await Assert.ThrowsAsync<AgentToolException>(() => _web.SearchAsync("x", TestContext.Current.CancellationToken));

        Assert.Contains("altavista", error.Message, StringComparison.Ordinal);
        Assert.Contains("duckduckgo", error.Message, StringComparison.Ordinal);
        Assert.Single(_tabs.Tabs);
    }

    // A captcha: the tab is shown to the user with a notification, and the search goes on once results appear.
    [Fact]
    public async Task Captcha_ShowsTheTabToTheUser_AndWaitsForResults()
    {
        Reply(Captcha, Captcha, TwoHits);

        var hits = await _web.SearchAsync("kafka", TestContext.Current.CancellationToken);

        Assert.Equal(2, hits.Count);
        Assert.True(_tabs.Active!.IsAgent);
        Assert.Equal(1, _panelShown);
        Assert.Contains("Поисковик просит подтвердить", Assert.Single(_notifications.Shown).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Captcha_NobodyAnswers_TheAgentIsToldToAskTheUser()
    {
        Reply([.. Enumerable.Repeat(Captcha, 100)]);

        var error = await Assert.ThrowsAsync<AgentToolException>(() => _web.SearchAsync("kafka", TestContext.Current.CancellationToken));

        Assert.Contains("требует человека", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_OpensThePage_AndReturnsItsTextAsMarkdown()
    {
        _tabs.Open(BrowserTabOwner.Agent, select: false);

        var text = await _web.ReadAsync(new Uri("https://example.org/docs"), TestContext.Current.CancellationToken);

        Assert.Equal("# Вход\n\nВведите почту.", text);
        Assert.Equal(3, _tabs.Tabs.Length);
    }

    private FakeBrowserPage SearchPage => _engine.Pages[^1];

    // The replies of the results script, in order; the tab is made by the first search.
    private void Reply(params string[] replies)
    {
        var queue = new Queue<string>(replies);
        _engine.Configure = page => page.ScriptReplies[BrowserSearchScript.Results] = queue;
    }
}
