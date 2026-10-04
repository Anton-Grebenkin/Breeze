using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Http;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// Reasoning in the <c>reasoning</c> field (Grok via ProxyAPI): the stream is rewritten so the library sees
/// <c>reasoning_content</c>; the encrypted <c>reasoning_details</c> is dropped.
/// </summary>
public sealed class ReasoningFieldTests
{
    private const string Stream = """
        data: {"id":"1","object":"chat.completion.chunk","created":0,"model":"m","choices":[{"index":0,"delta":{"role":"assistant","content":"","reasoning":"Сравниваю версии.","reasoning_details":[{"type":"reasoning.encrypted","data":"xxx"}]},"finish_reason":null}]}

        data: {"id":"1","object":"chat.completion.chunk","created":0,"model":"m","choices":[{"index":0,"delta":{"content":"Ответ."},"finish_reason":null}]}

        data: {"id":"1","object":"chat.completion.chunk","created":0,"model":"m","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: [DONE]


        """;

    [Fact]
    public void RewriteLine_MovesReasoning_DropsDetails_LeavesOtherLines()
    {
        var line = """data: {"choices":[{"delta":{"content":"","reasoning":"думаю","reasoning_details":[{"data":"x"}]}}]}""";

        Assert.Equal("""data: {"choices":[{"delta":{"reasoning_content":"думаю"}}]}""", ReasoningFieldStream.RewriteLine(line, keepReasoning: true));
        Assert.Equal("""data: {"choices":[{"delta":{}}]}""", ReasoningFieldStream.RewriteLine(line, keepReasoning: false));
        Assert.Equal("""data: {"choices":[{"delta":{"reasoning_content":"The"}}]}""", ReasoningFieldStream.RewriteLine("""data: {"choices":[{"delta":{"content":"","reasoning_content":"The"}}]}""", keepReasoning: true));
        Assert.Equal("""data: {"choices":[{"delta":{"content":"текст"}}]}""", ReasoningFieldStream.RewriteLine("""data: {"choices":[{"delta":{"content":"текст"}}]}""", keepReasoning: true));
        Assert.Equal("data: [DONE]", ReasoningFieldStream.RewriteLine("data: [DONE]", keepReasoning: true));
    }

    [Fact]
    public async Task Grok_ShowsReasoningFromTheReasoningField()
    {
        var (reasoning, text) = await StreamAsync("x-ai/grok-4.7");

        Assert.Equal("Сравниваю версии.", reasoning);
        Assert.Equal("Ответ.", text);
    }

    // A think-aloud model: the feed hides the proxy's summary because the tagged reasoning takes its place.
    [Fact]
    public async Task ThinkAloudModel_DropsTheSummary()
    {
        var (reasoning, text) = await StreamAsync("mistralai/devstral-2512");

        Assert.Equal(string.Empty, reasoning);
        Assert.Equal("Ответ.", text);
    }

    // Guards against storing a reply as hundreds of parts: Grok sends empty text in every reasoning chunk.
    [Fact]
    public async Task StreamedReasoning_IsStoredAsOnePart()
    {
        using var fixture = new AgentFixture();
        using var server = new FakeModelServer();
        server.Connect(fixture, new AgentOptions { Model = "x-ai/grok-4.7", AutoMemory = false });
        server.Enqueue(FakeModelServer.Reasoning("r1", "Думаю "), FakeModelServer.Reasoning("r2", "дальше."), FakeModelServer.Message("m1", "Ответ."));

        await fixture.SendAsync("вопрос");

        var session = await fixture.Conversation.SerializeSessionAsync(TestContext.Current.CancellationToken);
        var answer = session!.Value.GetProperty("stateBag").GetProperty("InMemoryChatHistoryProvider").GetProperty("messages").EnumerateArray().Last();
        Assert.Single(answer.GetProperty("contents").EnumerateArray(), part => part.GetProperty("$type").GetString() == "reasoning");
    }

    private static async Task<(string Reasoning, string Text)> StreamAsync(string model)
    {
        var agentOptions = new AgentOptions { Model = model };
        var clientOptions = OpenAIChatClientFactory.ClientOptions(agentOptions, new Uri("https://proxy.test/v1"));
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(new StreamingHandler(Stream)));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        using var client = OpenAIChatClientFactory.Create(agentOptions, new ApiKeyCredential("test"), clientOptions);

        var reasoning = new StringBuilder();
        var text = new StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "вопрос")], cancellationToken: TestContext.Current.CancellationToken))
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent thought:
                        reasoning.Append(thought.Text);
                        break;
                    case TextContent part:
                        text.Append(part.Text);
                        break;
                }
            }
        }

        return (reasoning.ToString(), text.ToString());
    }

    private sealed class StreamingHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body.Replace("\r\n", "\n", StringComparison.Ordinal), Encoding.UTF8, "text/event-stream"),
            });
    }
}
