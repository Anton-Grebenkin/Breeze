using System.Net;
using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Services.Web;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>
/// Page reading for <c>web_fetch</c>: the main HTML content as Markdown-like text without scripts or navigation; site
/// errors and non-text pages become explanations for the model.
/// </summary>
public sealed class WebPageTests
{
    private static readonly Uri Page = new("https://learn.example.com/docs/ef/bulk");

    private const string Html = """
        <html><head><title>Bulk update — EF Core</title><style>.x{}</style><script>alert('нет')</script></head>
        <body>
          <nav><a href="/">Главная</a></nav>
          <main>
            <h1>Массовое обновление</h1>
            <p>Метод <code>ExecuteUpdate</code> обновляет строки без загрузки.</p>
            <ul><li>Быстро</li><li>Без <a href="../tracking">отслеживания</a></li></ul>
            <pre><code>context.Orders
            .Where(o =&gt; o.Old)
            .ExecuteUpdate(s =&gt; s.SetProperty(o =&gt; o.Done, true));</code></pre>
            <table><tr><th>Версия</th><th>Есть</th></tr><tr><td>7</td><td>да</td></tr></table>
          </main>
          <footer>© Подвал</footer>
        </body></html>
        """;

    [Fact]
    public void Html_MainContentAsMarkdown()
    {
        var text = HtmlText.ToText(Html, Page);

        Assert.StartsWith("# Bulk update — EF Core\n\n# Массовое обновление", text, StringComparison.Ordinal);
        Assert.Contains("Метод `ExecuteUpdate` обновляет строки без загрузки.", text, StringComparison.Ordinal);
        Assert.Contains("- Быстро", text, StringComparison.Ordinal);
        Assert.Contains("[отслеживания](https://learn.example.com/docs/tracking)", text, StringComparison.Ordinal);
        Assert.Contains("```\ncontext.Orders\n    .Where(o => o.Old)", text, StringComparison.Ordinal);
        Assert.Contains("Версия | Есть |", text, StringComparison.Ordinal);
        Assert.DoesNotContain("alert", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Главная", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Подвал", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_HtmlAsText_AtTheFinalAddress()
    {
        using var reader = new WebPageReader(new PageServer(HttpStatusCode.OK, Html, "text/html; charset=utf-8", redirectTo: Page));

        var page = await reader.ReadAsync(new Uri("https://learn.example.com/short"), TestContext.Current.CancellationToken);

        Assert.Equal(Page, page.Url);
        Assert.Contains("# Массовое обновление", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_TextAndJson_AsIs()
    {
        using var reader = new WebPageReader(new PageServer(HttpStatusCode.OK, """{"version":"10.0.1"}""", "application/json"));

        var page = await reader.ReadAsync(Page, TestContext.Current.CancellationToken);

        Assert.Equal("""{"version":"10.0.1"}""", page.Text);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "text/html", "404")]
    [InlineData(HttpStatusCode.OK, "image/png", "image/png")]
    public async Task Reader_ErrorsAndNonText_AreExplained(HttpStatusCode status, string type, string expected)
    {
        using var reader = new WebPageReader(new PageServer(status, "x", type));

        var error = await Assert.ThrowsAsync<AgentToolException>(() => reader.ReadAsync(Page, TestContext.Current.CancellationToken));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_OnlyHttp() =>
        await Assert.ThrowsAsync<AgentToolException>(() => new WebPageReader(new PageServer(HttpStatusCode.OK, "x", "text/plain")).ReadAsync(new Uri("file:///C:/secret.txt"), TestContext.Current.CancellationToken));

    /// <summary>One typed response; a redirect shows up as the response's request URI, as in a real client.</summary>
    private sealed class PageServer(HttpStatusCode status, string body, string type, Uri? redirectTo = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (redirectTo is not null)
            {
                request.RequestUri = redirectTo;
            }

            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(type);
            return Task.FromResult(new HttpResponseMessage(status) { Content = content, RequestMessage = request });
        }
    }
}
