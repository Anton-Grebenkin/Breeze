using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CodeEditor.Modules.Agent.Services.Cache;
using CodeEditor.Modules.Agent.Services.Conversation;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Tests.Cache;

/// <summary>
/// One-hour Claude cache warmup (ADR 0022): one warmup request per idle period over 4 minutes; a new request or chat
/// cancels it, and a miss or failure stops further warmups. The test controls time.
/// </summary>
public sealed class CacheWarmupTests
{
    private static readonly TimeSpan Delay = CacheWarmupChatClient.DefaultDelay;

    private readonly ManualTimeProvider _time = new();
    private readonly FakeWarmer _warmer = new();
    private readonly HelperUsage _usage = new();
    private readonly SignalLogger _log = new();

    [Fact]
    public async Task IdleFourMinutes_WarmsUpOnce_WithAnswerAndPlaceholder()
    {
        using var client = Client();
        await AskAsync(client);

        _time.Advance(Delay - TimeSpan.FromSeconds(1));
        Assert.Equal(0, _warmer.Calls);
        _time.Advance(TimeSpan.FromSeconds(1));
        var messages = await _warmer.NextAsync();
        await _log.WaitAsync("warmed up");
        _time.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(["вопрос", "Читаю."], messages.SkipLast(1).Select(message => message.Text));
        var placeholder = messages[^1];
        Assert.Equal(ChatRole.User, placeholder.Role);
        Assert.Equal("c3", Assert.Single(placeholder.Contents.OfType<FunctionResultContent>()).CallId);
        Assert.Equal(1, _warmer.Calls);
        Assert.Equal(new HelperUsageTotals(10_000, 1, 9_500), _usage.Take());
    }

    [Fact]
    public async Task NextRequestWithinFourMinutes_CancelsTheWarmUp()
    {
        using var client = Client();
        await AskAsync(client, "первый");
        _time.Advance(TimeSpan.FromMinutes(3));
        await AskAsync(client, "второй");

        _time.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal(0, _warmer.Calls);
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal("второй", (await _warmer.NextAsync())[0].Text);
    }

    // A new chat doesn't need the old history warmed up.
    [Fact]
    public async Task Cancel_DropsThePendingWarmUp()
    {
        using var client = Client();
        await AskAsync(client);

        client.Cancel();
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(0, _warmer.Calls);
    }

    // Less than half read from the cache means the history diverged, so warmups only cost money.
    [Fact]
    public async Task MissedWarmUp_StopsFurtherWarmUps()
    {
        _warmer.Usage = new UsageDetails { InputTokenCount = 10_000, CachedInputTokenCount = 1_000, OutputTokenCount = 1 };
        using var client = Client();
        await AskAsync(client);
        _time.Advance(Delay);
        await _log.WaitAsync("missed the cache");

        await AskAsync(client);
        _time.Advance(Delay);

        Assert.Equal(1, _warmer.Calls);
    }

    [Fact]
    public async Task FailedWarmUp_StopsFurtherWarmUps()
    {
        _warmer.Failure = new InvalidOperationException("max_tokens is too small");
        using var client = Client();
        await AskAsync(client);
        _time.Advance(Delay);
        await _log.WaitAsync("failed");

        await AskAsync(client);
        _time.Advance(Delay);

        Assert.Equal(1, _warmer.Calls);
    }

    // An answer slower than 4.5 minutes may outlive its cache entry, and a warmup would rewrite the whole history.
    [Fact]
    public async Task SlowAnswer_IsNotWarmedUp()
    {
        using var client = Client(answerTime: TimeSpan.FromMinutes(5));
        await AskAsync(client);

        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(0, _warmer.Calls);
    }

    private CacheWarmupChatClient Client(TimeSpan? answerTime = null) =>
        new(new AnswerClient(_time, answerTime ?? TimeSpan.Zero), _warmer, _usage, _time, _log);

    private static async Task AskAsync(IChatClient client, string text = "вопрос") =>
        await client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, text)], new ChatOptions(), TestContext.Current.CancellationToken)
            .ToChatResponseAsync(TestContext.Current.CancellationToken);

    // Background warmup work gets 10 seconds, so a broken test fails instead of hanging.
    private static Task<T> Within<T>(ValueTask<T> pending) =>
        pending.AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    /// <summary>Model answering with text and a call; the answer time advances the test clock.</summary>
    private sealed class AnswerClient(ManualTimeProvider time, TimeSpan answerTime) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            time.Advance(answerTime);
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("Читаю."), new FunctionCallContent("c3", "read_file")]);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    /// <summary>Offline warmer: records each warmup's history and returns the preset usage or failure.</summary>
    private sealed class FakeWarmer : IPromptCacheWarmer
    {
        private readonly Channel<IReadOnlyList<ChatMessage>> _calls = Channel.CreateUnbounded<IReadOnlyList<ChatMessage>>();
        private int _count;

        public bool CanWarmUp => true;

        public UsageDetails Usage { get; set; } = new() { InputTokenCount = 10_000, CachedInputTokenCount = 9_500, OutputTokenCount = 1 };

        public Exception? Failure { get; set; }

        public int Calls => Volatile.Read(ref _count);

        public Task<UsageDetails?> WarmUpAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            _calls.Writer.TryWrite(messages);
            return Failure is null ? Task.FromResult<UsageDetails?>(Usage) : Task.FromException<UsageDetails?>(Failure);
        }

        public Task<IReadOnlyList<ChatMessage>> NextAsync() => Within(_calls.Reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>A log the test can await, since the warmup writes to it in the background.</summary>
    private sealed class SignalLogger : ILogger
    {
        private readonly Channel<string> _entries = Channel.CreateUnbounded<string>();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _entries.Writer.TryWrite(formatter(state, exception));

        public async Task WaitAsync(string fragment)
        {
            while (!(await Within(_entries.Reader.ReadAsync(TestContext.Current.CancellationToken))).Contains(fragment, StringComparison.Ordinal))
            {
            }
        }
    }
}
