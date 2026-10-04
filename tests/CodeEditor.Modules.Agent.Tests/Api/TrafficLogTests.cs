using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Http;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>Model traffic log: request body and response as is, without the headers carrying the key.</summary>
public sealed class TrafficLogTests : IDisposable
{
    private const string Stream = "data: {\"id\":\"c1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"m\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"ответ\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "codeeditor-traffic-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task StreamedExchange_IsWrittenWithoutHeaders()
    {
        var options = new AgentOptions { Model = "x-ai/grok-4.7" };
        var clientOptions = OpenAIChatClientFactory.ClientOptions(options, new Uri("https://proxy.test/v1"));
        clientOptions.AddPolicy(new TrafficLogPolicy(_folder, TimeProvider.System), PipelinePosition.PerTry);
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(new StreamHandler()));
        using (var client = OpenAIChatClientFactory.Create(options, new ApiKeyCredential("secret-key"), clientOptions))
        {
            var response = await client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "вопрос")], cancellationToken: TestContext.Current.CancellationToken)
                .ToChatResponseAsync(TestContext.Current.CancellationToken);
            Assert.Equal("ответ", response.Text);
        }

        var log = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_folder)), TestContext.Current.CancellationToken);
        Assert.StartsWith("POST https://proxy.test/v1/chat/completions", log, StringComparison.Ordinal);
        Assert.Contains("\"content\":\"вопрос\"", log, StringComparison.Ordinal);
        Assert.Contains("--- 200", log, StringComparison.Ordinal);
        Assert.Contains("\"content\":\"ответ\"", log, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", log, StringComparison.Ordinal);
    }

    private sealed class StreamHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Stream, Encoding.UTF8, "text/event-stream") });
    }
}
