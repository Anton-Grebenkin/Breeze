using System.Runtime.CompilerServices;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Services.Conversation;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Models;

/// <summary>
/// A chat that switches models: reasoning goes back only to the model and service that wrote it, images only to a
/// model with vision, and the history itself keeps everything.
/// </summary>
public sealed class ModelHistoryTests
{
    private const string Service = "https://api.example.test/v1";
    private const string Claude = "anthropic/claude-sonnet-5";
    private const string Gpt = "openai/gpt-6-luna";

    private readonly CapturingClient _inner = new();

    [Fact]
    public async Task ReasoningOfAnotherModel_IsLeftOut_OwnIsKept_HistoryUntouched()
    {
        var own = Reasoning("думаю", "EqoBsig", ModelHistoryChatClient.Origin(Service, Claude));
        var gpt = Reasoning(string.Empty, "gAAAAAenc", ModelHistoryChatClient.Origin(Service, Gpt));
        var otherService = Reasoning(string.Empty, "EqoBother", ModelHistoryChatClient.Origin("https://other.test/v1", Claude));
        var answer = new ChatMessage(ChatRole.Assistant, [gpt, otherService, own, new TextContent("ответ")]);

        await SendAsync(Claude, answer);

        Assert.Equal([own, answer.Contents[^1]], Sent().Contents);
        Assert.Equal(4, answer.Contents.Count);
    }

    // Chats from before the stamps: GPT's encrypted reasoning is a Fernet token, Claude's signature is not.
    [Theory]
    [InlineData(Gpt, "gAAAAAenc", true)]
    [InlineData(Gpt, "EqoBsig", false)]
    [InlineData(Claude, "EqoBsig", true)]
    [InlineData(Claude, "gAAAAAenc", false)]
    [InlineData("deepseek/deepseek-chat", "EqoBsig", false)]
    public async Task UnstampedReasoning_GoesByItsLook(string model, string signature, bool kept)
    {
        await SendAsync(model, new ChatMessage(ChatRole.Assistant, [Reasoning("думаю", signature, origin: null), new TextContent("ответ")]));

        Assert.Equal(kept, Sent().Contents.OfType<TextReasoningContent>().Any());
    }

    [Fact]
    public async Task ImagesForModelWithoutVision_BecomeANote()
    {
        var question = new ChatMessage(ChatRole.User, [new TextContent("что на снимке?"), new DataContent(new byte[] { 1, 2, 3 }, "image/png")]);

        await SendAsync("test/model", question);

        Assert.Equal(["что на снимке?", Strings.ImageNotForModel], Sent().Contents.OfType<TextContent>().Select(text => text.Text));
        Assert.IsType<DataContent>(question.Contents[1]);
    }

    [Fact]
    public async Task ReasoningInTheResponse_IsStampedWithItsOrigin()
    {
        var updates = await SendAsync(Claude, new ChatMessage(ChatRole.User, "вопрос"));

        var reasoning = Assert.Single(updates.SelectMany(update => update.Contents).OfType<TextReasoningContent>());
        Assert.Equal(ModelHistoryChatClient.Origin(Service, Claude), reasoning.AdditionalProperties![ModelHistoryChatClient.OriginKey]);
    }

    private static TextReasoningContent Reasoning(string text, string signature, string? origin) =>
        new(text) { ProtectedData = signature, AdditionalProperties = origin is null ? null : new() { [ModelHistoryChatClient.OriginKey] = origin } };

    private async Task<List<ChatResponseUpdate>> SendAsync(string model, ChatMessage message)
    {
        using var client = new ModelHistoryChatClient(_inner, Service, model);
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([message], cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        return updates;
    }

    private ChatMessage Sent() => Assert.Single(_inner.Messages!);

    /// <summary>Records the request and answers with reasoning and text.</summary>
    private sealed class CapturingClient : IChatClient
    {
        public List<ChatMessage>? Messages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Messages = [.. messages];
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent("размышляю") { ProtectedData = "EqoBnew" }]);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ответ");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
