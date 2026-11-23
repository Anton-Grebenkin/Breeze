using System.ClientModel;
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

    // A dropped connection or a 502 left after the library's own retries, before any content: the request is repeated
    // and each pause is reported, so the feed can show a countdown.
    [Fact]
    public async Task ServiceFailureBeforeContent_IsRetried_AndThePauseIsReported()
    {
        var inner = new ScriptedClient([Started(), null], [null], [Text("ответ")]);
        inner.Failures.Enqueue(new IOException("reset"));
        inner.Failures.Enqueue(new ClientResultException("no answer"));
        var retries = new RequestRetries();
        var reported = new List<RequestRetry>();
        retries.Waiting += (_, retry) => reported.Add(retry);
        var client = new StreamFailureChatClient(inner, [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero], retries, error => "сбой: " + error.Message);

        var response = await client.GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken).ToChatResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ответ", response.Text);
        Assert.Equal(3, inner.Calls);
        Assert.Equal([("сбой: reset", 1, 3), ("сбой: no answer", 2, 3)], reported.Select(retry => (retry.Reason, retry.Attempt, retry.Attempts)));
    }

    // An error inside the stream may be the service's verdict on the request: two quick retries, as before.
    [Fact]
    public async Task StreamError_HasTwoRetries_HoweverManyPausesThereAre()
    {
        var inner = new ScriptedClient([RateLimited()], [RateLimited()], [RateLimited()], [Text("не дойдёт")]);
        var pauses = Enumerable.Repeat(TimeSpan.Zero, 4).ToArray();

        await Assert.ThrowsAsync<ModelStreamException>(() =>
            new StreamFailureChatClient(inner, pauses).GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken)
                .ToChatResponseAsync(TestContext.Current.CancellationToken));

        Assert.Equal(3, inner.Calls);
    }

    // A failure of the request itself (here a bug) is not repeated: the answer would not change.
    [Fact]
    public async Task OtherFailure_IsNotRetried()
    {
        var inner = new ScriptedClient([null], [Text("не дойдёт")]);
        inner.Failures.Enqueue(new InvalidOperationException("bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StreamFailureChatClient(inner, TwoRetries).GetStreamingResponseAsync([], cancellationToken: TestContext.Current.CancellationToken)
                .ToChatResponseAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, inner.Calls);
    }

    private static ChatResponseUpdate Started() => new() { Role = ChatRole.Assistant };

    private static ChatResponseUpdate Text(string text) => new(ChatRole.Assistant, text);

    private static ChatResponseUpdate RateLimited() =>
        new(ChatRole.Assistant, [new ErrorContent("openai/gpt-6.1-sol is temporarily rate-limited upstream.") { ErrorCode = "429" }]);

    /// <summary>
    /// Fake model with one stream per call; <c>null</c> in a stream throws the next of <see cref="Failures"/>, or a
    /// stream error.
    /// </summary>
    private sealed class ScriptedClient(params ChatResponseUpdate?[][] streams) : IChatClient
    {
        public int Calls { get; private set; }

        public Queue<Exception> Failures { get; } = new();

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var stream = streams[Calls++];
            foreach (var update in stream)
            {
                await Task.Yield();
                yield return update ?? throw (Failures.TryDequeue(out var failure) ? failure : new ModelStreamException("Request failed (api_error)"));
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
