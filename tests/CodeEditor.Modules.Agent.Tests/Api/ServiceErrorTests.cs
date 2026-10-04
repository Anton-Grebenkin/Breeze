using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// Service errors keep the service's text: an error event in a 200 stream fails the turn instead of giving an empty
/// answer; the error body of a streaming request is read, and a 403 "top up required" isn't reported as a bad key.
/// </summary>
public sealed class ServiceErrorTests
{
    private const string ErrorEvent =
        "event: error\ndata: {\"type\":\"error\",\"sequence_number\":0,\"code\":\"invalid_request\",\"message\":\"store is not supported\",\"param\":\"store\"}\n\n";

    private const string FailedEvent =
        "event: response.failed\ndata: {\"type\":\"response.failed\",\"sequence_number\":0,\"response\":{\"id\":\"r1\",\"object\":\"response\"," +
        "\"created_at\":1,\"status\":\"failed\",\"model\":\"m\",\"output\":[],\"parallel_tool_calls\":true,\"tool_choice\":\"auto\",\"tools\":[]," +
        "\"error\":{\"code\":\"server_error\",\"message\":\"include is not supported\"}}}\n\n";

    [Theory]
    [InlineData(ErrorEvent, "store is not supported (invalid_request)")]
    [InlineData(FailedEvent, "include is not supported (server_error)")]
    public async Task ErrorEventInStream_FailsTheTurn_WithTheServiceText(string stream, string message)
    {
        var error = await Assert.ThrowsAsync<ModelStreamException>(() => StreamAsync(HttpStatusCode.OK, stream, Gpt));

        Assert.Equal(message, error.Message);
        Assert.Contains(message, AgentErrors.Describe(error, Gpt), StringComparison.Ordinal);
    }

    // Provod puts the code and text at the root, OpenAI and ProxyAPI under "error".
    [Theory]
    [InlineData("openai/gpt-6-luna", """{"code":"FIRST_TOP_UP_REQUIRED","message":"Top up the balance to start.","statusCode":403}""")]
    [InlineData("x-ai/grok-4.7", """{"error":{"code":"FIRST_TOP_UP_REQUIRED","message":"Top up the balance to start."}}""")]
    public async Task Forbidden_WithReasonInBody_ShowsTheReason(string model, string body)
    {
        var options = new AgentOptions { Model = model };

        var error = await Assert.ThrowsAsync<ClientResultException>(() => StreamAsync(HttpStatusCode.Forbidden, body, options));

        Assert.Contains("Top up the balance to start. (FIRST_TOP_UP_REQUIRED)", AgentErrors.Describe(error, options), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid key"}}""")]
    public async Task KeyProblems_SayTheKeyWasRejected(HttpStatusCode status, string body)
    {
        var error = await Assert.ThrowsAsync<ClientResultException>(() => StreamAsync(status, body, Gpt));

        Assert.Equal(Strings.ErrorApiKeyRejected, AgentErrors.Describe(error, Gpt));
    }

    // The log gets the body as is, even if it isn't OpenAI-shaped, but no longer than 500 characters.
    [Fact]
    public async Task Body_ForTheLog_IsRawAndShort()
    {
        var page = "<html>" + new string('x', 1_000) + "</html>";

        var error = await Assert.ThrowsAsync<ClientResultException>(() => StreamAsync(HttpStatusCode.BadGateway, page, Gpt));

        Assert.Equal(page[..500], ServiceErrorText.Body(error));
        Assert.Null(ServiceErrorText.Of(error));
    }

    // Guards against OpenRouter ending a stream with finish_reason "error", which crashed the library with
    // "Unknown ChatFinishReason value"; now it's a turn error.
    [Theory]
    [InlineData("""{"id":"gen-1","object":"chat.completion.chunk","created":1,"model":"m","choices":[{"index":0,"delta":{"content":""},"finish_reason":"error"}]}""", "finish_reason: error")]
    [InlineData("""{"id":"gen-1","object":"chat.completion.chunk","created":1,"model":"m","error":{"code":502,"message":"Provider returned error"},"choices":[{"index":0,"delta":{"content":""},"finish_reason":"error"}]}""", "Provider returned error (502)")]
    public async Task ErrorFinishInCompletionsStream_FailsTheTurn_WithTheServiceText(string chunk, string message)
    {
        const string reasoning = """{"id":"gen-1","object":"chat.completion.chunk","created":1,"model":"m","choices":[{"index":0,"delta":{"content":"","reasoning":"Думаю"},"finish_reason":null}]}""";
        var options = new AgentOptions { Endpoint = AgentServices.AitunnelEndpoint, Model = "xiaomi/mimo-v2.6-pro" };

        var error = await Assert.ThrowsAsync<ModelStreamException>(() => StreamAsync(HttpStatusCode.OK, $"data: {reasoning}\n\ndata: {chunk}\n\n", options));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void OrdinaryUpdate_IsNoFailure() =>
        Assert.Null(StreamFailureChatClient.Failure(new ChatResponseUpdate(ChatRole.Assistant, "text")));

    private static readonly AgentOptions Gpt = new() { Model = "openai/gpt-6-luna" };

    // A streaming request from the real client to a server that answers everything with the given status and body.
    private static async Task StreamAsync(HttpStatusCode status, string body, AgentOptions options)
    {
        var clientOptions = OpenAIChatClientFactory.ClientOptions(options, new Uri("https://proxy.test/v1"));
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(new Handler(status, body)));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        var client = OpenAIChatClientFactory.Create(options, new ApiKeyCredential("test"), clientOptions, streamRetryDelays: []);
        await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: TestContext.Current.CancellationToken))
        {
        }
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, status == HttpStatusCode.OK ? "text/event-stream" : "application/json") });
    }
}
