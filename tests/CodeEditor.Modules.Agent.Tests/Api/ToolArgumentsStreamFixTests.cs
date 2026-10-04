using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Http;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// A stray "{}" before tool call arguments (Provod, Claude with reasoning): the real client receives a stream in the
/// logged format and the calls still arrive with their arguments.
/// </summary>
public sealed class ToolArgumentsStreamFixTests
{
    [Fact]
    public async Task ParallelCalls_EmptyObjectBeforeArguments_IsDropped()
    {
        var stream = Stream(
            Text("Читаю оба файла."),
            Start(2, "toolu_1", "read_file"), Arguments(2, "{}"), Arguments(2, "{\"path\":"), Arguments(2, "\"a.cs\"}"),
            Start(3, "toolu_2", "read_file"), Arguments(3, "{}"), Arguments(3, "{\"path\":\"b.cs\"}"),
            Finish());

        var calls = await CallsAsync(stream);

        Assert.Equal(["a.cs", "b.cs"], calls.Select(call => call.Arguments!["path"]?.ToString()));
        Assert.All(calls, call => Assert.Null(call.Exception));
    }

    // A call without arguments: the held-back "{}" is released when the call ends.
    [Fact]
    public async Task CallWithoutArguments_KeepsEmptyObject()
    {
        var calls = await CallsAsync(Stream(Start(0, "toolu_1", "list_changes"), Arguments(0, "{}"), Finish()));

        var call = Assert.Single(calls);
        Assert.Null(call.Exception);
        Assert.Empty(call.Arguments!);
    }

    [Fact]
    public void OrdinaryStream_PassesAsIs()
    {
        var fix = new ToolArgumentsStreamFix();
        string[] lines = [Start(0, "call_1", "read_file"), string.Empty, Arguments(0, "{\"path\":\"a.cs\"}"), string.Empty, Finish(), "data: [DONE]"];

        Assert.Equal(lines, lines.Select(fix.Rewrite));
    }

    private static async Task<List<FunctionCallContent>> CallsAsync(string stream)
    {
        // Chat Completions stream: ProxyAPI serves Claude this way, Provod uses Messages.
        var options = new AgentOptions { Endpoint = AgentServices.ProxyApiEndpoint, Model = "anthropic/claude-sonnet-5" };
        var clientOptions = OpenAIChatClientFactory.ClientOptions(options, new Uri("https://proxy.test/v1"));
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(new StreamHandler(stream)));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        using var client = OpenAIChatClientFactory.Create(options, new ApiKeyCredential("test"), clientOptions);

        var response = await client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "вопрос")], cancellationToken: TestContext.Current.CancellationToken)
            .ToChatResponseAsync(TestContext.Current.CancellationToken);
        return [.. response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()];
    }

    private static string Stream(params string[] events) => string.Concat(events.Select(line => line + "\n\n")) + "data: [DONE]\n\n";

    private static string Text(string text) => Chunk(new { content = text, role = "assistant" }, finish: null);

    private static string Start(int index, string id, string name) =>
        Chunk(new { tool_calls = new[] { new { index, id, type = "function", function = new { name, arguments = string.Empty } } } }, finish: null);

    private static string Arguments(int index, string arguments) =>
        Chunk(new { tool_calls = new[] { new { index, type = "function", function = new { arguments } } } }, finish: null);

    private static string Finish() => Chunk(new { }, finish: "tool_calls");

    // Event as Provod sends it: id, object, created and model on every chunk.
    private static string Chunk(object delta, string? finish) => "data: " + JsonSerializer.Serialize(new
    {
        id = "msg_1",
        @object = "chat.completion.chunk",
        created = 1_790_922_645,
        model = "claude-sonnet-5",
        choices = new[] { new { delta, finish_reason = finish, index = 0 } },
    });

    private sealed class StreamHandler(string stream) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(stream, Encoding.UTF8, "text/event-stream") });
    }
}
