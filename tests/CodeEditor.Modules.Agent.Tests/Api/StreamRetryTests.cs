using System.Runtime.CompilerServices;
using CodeEditor.Modules.Agent.Services.Api;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// A stream that fails before any content is retried (e.g. "temporarily rate-limited upstream" right after the answer
/// starts); after content a retry would duplicate work, so it's a turn error.
/// </summary>
public sealed class StreamRetryTests
{
    private static readonly TimeSpan[] TwoRetries = [TimeSpan.Zero, TimeSpan.Zero];

    [Fact]
    public async Task FailureBeforeContent_IsRetried()
    {
        var inner = new ScriptedClient([Started(), RateLimited()], [Text("ответ")]);

        var response = await new StreamFailureChatClient(inner, TwoRetries).GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken)
            .ToChatResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ответ", response.Text);
        Assert.Equal(2, inner.Calls);
    }

    // The Messages client throws the stream error itself; it's retried the same way.
    [Fact]
    public async Task ThrownFailureBeforeContent_IsRetried()
    {
        var inner = new ScriptedClient([Started(), null], [Text("ответ")]);

        var response = await new StreamFailureChatClient(inner, TwoRetries).GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken)
            .ToChatResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ответ", response.Text);
        Assert.Equal(2, inner.Calls);
    }

    // Only reasoning arrived before the failure; with no text or calls, a retry duplicates nothing.
    [Fact]
    public async Task FailureAfterReasoningOnly_IsRetried()
    {
        var inner = new ScriptedClient([new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent("думаю")]), null], [Text("ответ")]);

        var response = await new StreamFailureChatClient(inner, TwoRetries).GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken)
            .ToChatResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ответ", response.Text);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task FailureAfterContent_FailsWithoutRetry()
    {
        var inner = new ScriptedClient([Text("начало"), RateLimited()], [Text("ответ")]);

        var error = await Assert.ThrowsAsync<ModelStreamException>(() =>
            new StreamFailureChatClient(inner, TwoRetries).GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken)
                .ToChatResponseAsync(TestContext.Current.CancellationToken));

        Assert.Contains("rate-limited", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task RepeatedFailure_GivesUpAfterRetries()
    {
        var inner = new ScriptedClient([RateLimited()], [RateLimited()], [RateLimited()], [Text("не дойдёт")]);

        await Assert.ThrowsAsync<ModelStreamException>(() =>
            new StreamFailureChatClient(inner, TwoRetries).GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken)
                .ToChatResponseAsync(TestContext.Current.CancellationToken));

        Assert.Equal(3, inner.Calls);
    }

    private static ChatResponseUpdate Started() => new() { Role = ChatRole.Assistant };

    private static ChatResponseUpdate Text(string text) => new(ChatRole.Assistant, text);

    private static ChatResponseUpdate RateLimited() =>
        new(ChatRole.Assistant, [new ErrorContent("openai/gpt-6.1-sol is temporarily rate-limited upstream.") { ErrorCode = "429" }]);

    /// <summary>Fake model with one stream per call; <c>null</c> in a stream throws a stream error.</summary>
    private sealed class ScriptedClient(params ChatResponseUpdate?[][] streams) : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var stream = streams[Calls++];
            foreach (var update in stream)
            {
                await Task.Yield();
                yield return update ?? throw new ModelStreamException("Request failed (api_error)");
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
