using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Web;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Conversation;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Services.Web;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>
/// Web access for the agent (ADR 0025): search is a separate fast-model request with AITUNNEL's server tool (AITUNNEL
/// only); unknown sites need a card while docs open at once; results come back as data in tags.
/// </summary>
public sealed class WebToolsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void SearchRequest_FastModel_ServerTool()
    {
        var body = WebSearchClient.Request("gpt-6-luna", "EF Core 10 ExecuteUpdate");

        Assert.Equal("gpt-6-luna", (string?)body["model"]);
        Assert.Equal(WebSearchClient.ServerTool, (string?)body["tools"]![0]!["type"]);
        Assert.Equal("EF Core 10 ExecuteUpdate", (string?)body["messages"]![1]!["content"]);
    }

    [Fact]
    public void SearchAnswer_SourcesFromCitations_WithoutRepeats()
    {
        using var completion = JsonDocument.Parse("""
            {"choices":[{"message":{"role":"assistant","content":"ExecuteUpdate есть с EF Core 7 [1].","annotations":[
              {"type":"url_citation","url_citation":{"url":"https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete","title":"ExecuteUpdate"}},
              {"type":"url_citation","url_citation":{"url":"https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete","title":"ExecuteUpdate"}}]}}]}
            """);

        var result = WebSearchClient.Parse(completion.RootElement);

        Assert.Equal("ExecuteUpdate есть с EF Core 7 [1].", result.Answer);
        Assert.Equal([new WebSource("ExecuteUpdate", "https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete")], result.Sources);
    }

    [Fact]
    public async Task Search_GoesToChatCompletionsOfTheService_UsageCounted()
    {
        _fixture.Options.Set(new AgentOptions { Endpoint = AgentServices.AitunnelEndpoint, Model = AgentFixture.TestModel });
        var server = new SearchServer("""{"choices":[{"message":{"content":"Ответ."}}],"usage":{"prompt_tokens":900,"completion_tokens":40,"prompt_tokens_details":{"cached_tokens":100}}}""");
        var search = new WebSearchClient(_fixture.Secrets, _fixture.Options, _fixture.HelperUsage, _fixture.UserData, TimeProvider.System, new HttpClientPipelineTransport(new HttpClient(server)));

        var result = await search.SearchAsync("запрос", TestContext.Current.CancellationToken);

        Assert.Equal("Ответ.", result.Answer);
        Assert.Equal("https://api.aitunnel.ru/v1/chat/completions", server.Uri!.AbsoluteUri);
        Assert.Equal("gpt-6-luna", (string?)server.Body!["model"]);
        Assert.Equal(new HelperUsageTotals(900, 40, 100), _fixture.HelperUsage.Take());
    }

    // Only AITUNNEL has server-side search, so other services get no search tool; page fetching works everywhere.
    [Fact]
    public void SearchTool_OnlyWhereTheServiceSearches()
    {
        _fixture.Options.Set(new AgentOptions { Endpoint = AgentServices.AitunnelEndpoint, Model = AgentFixture.TestModel });
        Assert.Equal([WebAgentTools.SearchName, WebAgentTools.FetchName], Tools().CreateTools().Select(tool => tool.Name));

        _fixture.Options.Set(new AgentOptions { Endpoint = AgentServices.ProxyApiEndpoint, Model = AgentFixture.TestModel });
        Assert.Equal([WebAgentTools.FetchName], Tools().CreateTools().Select(tool => tool.Name));
    }

    [Fact]
    public async Task FetchPreview_NamesThePage()
    {
        var preview = Assert.Single(await Tools().PreviewAsync(WebAgentTools.FetchName, new Dictionary<string, object?> { ["url"] = "https://example.org/a?b=1" }, TestContext.Current.CancellationToken));

        Assert.Equal(("Агент хочет открыть страницу", "example.org", "GET https://example.org/a?b=1"), (preview.Title, preview.Header, preview.NewText));
    }

    [Fact]
    public async Task Fetch_PageIsDataInTags()
    {
        using var reader = new WebPageReader(new SearchServer("Текст страницы.", "text/plain"));
        var fetch = new WebAgentTools(reader, Search(), _fixture.Outputs).CreateTools().OfType<AIFunction>().Single(tool => tool.Name == WebAgentTools.FetchName);

        var result = (await fetch.InvokeAsync(new AIFunctionArguments { ["url"] = "https://example.org/a" }, TestContext.Current.CancellationToken))?.ToString();

        Assert.Equal("<web_page url=\"https://example.org/a\">\nТекст страницы.\n</web_page>", result);
    }

    // Docs open at once; an unknown site asks with a card offering "Always allow" for that site.
    [Fact]
    public void Approvals_DocsAtOnce_OtherSitesAsk()
    {
        var settings = new FakeSettingsService();
        var approvals = new WebApprovals(_fixture.Options, settings, NullLogger<WebApprovals>.Instance);

        Assert.True(approvals.IsPreapproved(WebAgentTools.FetchName, Url("https://learn.microsoft.com/dotnet")));
        Assert.False(approvals.IsPreapproved(WebAgentTools.FetchName, Url("https://evil.example/?q=секрет")));
        Assert.Equal("evil.example", approvals.SuggestRule(WebAgentTools.FetchName, Url("https://evil.example/x")));
        approvals.AllowAlways(WebAgentTools.FetchName, "docs.example.org");
        Assert.Equal(new[] { "docs.example.org" }, settings.Written[WebApprovals.HostsKey]);
    }

    [Fact]
    public void FetchWorksInAskMode_SearchIsReadOnly()
    {
        var tools = Tools().CreateTools().ToList();

        Assert.All(tools, tool => Assert.True(ToolPolicy.Allows(AgentMode.Ask, tool)));
        Assert.True(ReadOnlyAIFunction.IsReadOnly(tools.Single(tool => tool.Name == WebAgentTools.SearchName)));
    }

    // A service without its own search (a local model): the search engine's results come from the editor's browser.
    [Fact]
    public async Task Search_WithoutServerSearch_GoesThroughTheBrowser()
    {
        _fixture.Options.Set(new AgentOptions { Endpoint = "http://localhost:11434/v1", Model = "qwen3" });
        var browser = new FakeWebBrowser { Hits = [new("Channel<T>", "https://learn.microsoft.com/c", "Каналы."), new("Blog", "https://blog.example/x", string.Empty)] };
        using var reader = new WebPageReader();
        var tools = new WebAgentTools(reader, Search(), _fixture.Outputs, browser).CreateTools().OfType<AIFunction>().ToList();

        var result = (await tools.Single(tool => tool.Name == WebAgentTools.SearchName).InvokeAsync(new AIFunctionArguments { ["query"] = "c# channel" }, TestContext.Current.CancellationToken))?.ToString();

        Assert.Equal(
            "<web_search query=\"c# channel\">\nВыдача поисковика, открытая в браузере редактора (без пересказа: источник читай через web_fetch):\n" +
            "1. Channel<T> — https://learn.microsoft.com/c\n   Каналы.\n2. Blog — https://blog.example/x\n</web_search>",
            result);
        Assert.Equal(["c# channel"], browser.Queries);

        // Without a browser such a service has no search tool at all, as before.
        Assert.DoesNotContain(new WebAgentTools(reader, Search(), _fixture.Outputs).CreateTools(), tool => tool.Name == WebAgentTools.SearchName);
    }

    // A site that turns a bare HTTP client away is read through the browser; the tag says so.
    [Fact]
    public async Task Fetch_RefusedByTheSite_IsReadThroughTheBrowser()
    {
        using var reader = new WebPageReader(new Refusing());
        var browser = new FakeWebBrowser { Page = "# Статья\n\nТекст." };
        var fetch = new WebAgentTools(reader, Search(), _fixture.Outputs, browser).CreateTools().OfType<AIFunction>().Single(tool => tool.Name == WebAgentTools.FetchName);

        var result = (await fetch.InvokeAsync(new AIFunctionArguments { ["url"] = "https://example.org/a" }, TestContext.Current.CancellationToken))?.ToString();

        Assert.Equal("<web_page url=\"https://example.org/a\" via=\"browser\">\n# Статья\n\nТекст.\n</web_page>", result);

        browser.Page = null;
        var error = await Assert.ThrowsAsync<AgentToolException>(async () => await fetch.InvokeAsync(new AIFunctionArguments { ["url"] = "https://example.org/a" }, TestContext.Current.CancellationToken));
        Assert.Contains("403", error.Message, StringComparison.Ordinal);
        Assert.Contains("Через браузер страница тоже не открылась", error.Message, StringComparison.Ordinal);
    }

    private WebAgentTools Tools() => new(new WebPageReader(), Search(), _fixture.Outputs);

    private WebSearchClient Search() => new(_fixture.Secrets, _fixture.Options, _fixture.HelperUsage, _fixture.UserData, TimeProvider.System);

    private static Dictionary<string, object?> Url(string url) => new() { ["url"] = url };

    private sealed class FakeWebBrowser : IWebBrowserAccess
    {
        public IReadOnlyList<WebSearchHit> Hits { get; init; } = [];

        public string? Page { get; set; }

        public List<string> Queries { get; } = [];

        public Task<IReadOnlyList<WebSearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(Hits);
        }

        public Task<string> ReadAsync(Uri url, CancellationToken cancellationToken) =>
            Page is { } page ? Task.FromResult(page) : Task.FromException<string>(new AgentToolException("нет сети"));
    }

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { RequestMessage = request });
    }

    /// <summary>Records the request URI and body and replies with the given text.</summary>
    private sealed class SearchServer(string body, string type = "application/json") : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public JsonObject? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            if (request.Content is not null)
            {
                Body = JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
            }

            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(type);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request };
        }
    }
}
