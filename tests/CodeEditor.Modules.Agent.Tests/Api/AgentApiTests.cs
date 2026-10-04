using System.ClientModel;
using System.ClientModel.Primitives;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>API protocol choice by model and readable service error messages.</summary>
public sealed class AgentApiTests
{
    [Theory]
    [InlineData("openai/gpt-6-luna", AgentApi.Auto, true)]
    [InlineData("openai/gpt-5-mini", AgentApi.Auto, true)]
    [InlineData("anthropic/claude-sonnet-5", AgentApi.Auto, false)]
    [InlineData("deepseek/deepseek-v4-flash", AgentApi.Auto, false)]
    [InlineData("openai/gpt-6-luna", AgentApi.ChatCompletions, false)]
    [InlineData("qwen/qwen3-coder", AgentApi.Responses, true)]
    public void Protocol_IsChosenByModel_OrSetting(string model, AgentApi api, bool responses) =>
        Assert.Equal(responses, OpenAIChatClientFactory.UsesResponses(new AgentOptions { Model = model, Api = api }));

    [Fact]
    public async Task Responses_AreNotStoredOnServer_AndConversationIdStaysInside()
    {
        using var inner = new RecordingClient();
        using var client = new StatelessResponsesChatClient(inner);

        var response = await client.GetResponseAsync("вопрос", new ChatOptions { ConversationId = "resp_0" }, TestContext.Current.CancellationToken);

        Assert.Null(response.ConversationId);
        Assert.Null(inner.Options!.ConversationId);
        // The Responses API options type is experimental, so the property is read by name.
        var raw = inner.Options.RawRepresentationFactory!(inner)!;
        Assert.Equal(false, raw.GetType().GetProperty("StoredOutputEnabled")!.GetValue(raw));
    }

    [Theory]
    [InlineData(402, "не хватает на этот запрос (402)")]
    [InlineData(429, "ограничил частоту")]
    public void PaymentAndRateLimit_HaveOwnMessages(int status, string expected)
    {
        var error = new ClientResultException(new FakeResponse(status));

        Assert.Contains(expected, AgentErrors.Describe(error, new AgentOptions()), StringComparison.Ordinal);
    }

    private sealed class RecordingClient : IChatClient
    {
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")) { ConversationId = "resp_1" });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class FakeResponse(int status) : PipelineResponse
    {
        public override int Status => status;

        public override string ReasonPhrase => string.Empty;

        public override Stream? ContentStream { get; set; }

        public override BinaryData Content => BinaryData.Empty;

        protected override PipelineResponseHeaders HeadersCore => throw new NotSupportedException();

        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => BinaryData.Empty;

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(BinaryData.Empty);

        public override void Dispose()
        {
        }
    }
}
