using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Cache;

/// <summary>
/// Warms the Claude cache for an hour while the conversation waits (ADR 0022). A cache entry lives 5 minutes, but the
/// conversation often idles: the user reads the reply, ponders an approval card, a long build runs. After such a pause
/// the next request pays for the whole history again, plus the write markup (×1.25). Writing every step to the one-hour
/// cache costs a third more per turn (×2 on all new input), so instead: if no request follows within 4 minutes, one
/// warm-up request goes out with the same history plus the model reply and a stub, one-hour marks and a one-token
/// answer. It reads history from cache (10% of input price) and writes only the reply for an hour, so the next request
/// finds the entry within the hour. One warm-up per pause. After a miss there are no more warm-ups for the client's
/// lifetime: the history diverged from the cache and warming would only cost money.
/// </summary>
internal sealed partial class CacheWarmupChatClient(
    IChatClient inner, IPromptCacheWarmer warmer, HelperUsage usage, TimeProvider time, ILogger logger, TimeSpan? delay = null)
    : DelegatingChatClient(inner)
{
    /// <summary>A cache entry lives 5 minutes from the request; one minute is slack for the service queue.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMinutes(4);

    /// <summary>A response slower than the delay plus this margin may have outlived its entry; warm-up would not extend it.</summary>
    private static readonly TimeSpan LateMargin = TimeSpan.FromSeconds(30);

    private const string PendingResult = "pending";
    private const string PlaceholderText = "…";

    private readonly TimeSpan _delay = delay ?? DefaultDelay;
    private readonly Lock _gate = new();
    private CancellationTokenSource? _pending;
    private volatile bool _missed;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = Begin(messages, options);
        var response = await base.GetResponseAsync(request.History, options, cancellationToken);
        Schedule(request, response.Messages);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = Begin(messages, options);
        List<ChatResponseUpdate> updates = [];
        await foreach (var update in base.GetStreamingResponseAsync(request.History, options, cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }

        Schedule(request, updates.ToChatResponse().Messages);
    }

    /// <summary>The conversation continued or a new one started: the pending warm-up is not needed.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            _pending?.Cancel();
            _pending?.Dispose();
            _pending = null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cancel();
        }

        base.Dispose(disposing);
    }

    // History is copied: the warm-up 4 minutes later must repeat the request byte for byte.
    private Request Begin(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        Cancel();
        return new Request([.. messages], options?.Clone(), time.GetUtcNow());
    }

    private void Schedule(Request request, IList<ChatMessage> answer)
    {
        var elapsed = time.GetUtcNow() - request.Started;
        if (_missed || !answer.Any(HasContent) || elapsed > _delay + LateMargin)
        {
            return;
        }

        List<ChatMessage> messages = [.. request.History, .. answer, Placeholder(answer)];
        var pending = new CancellationTokenSource();
        lock (_gate)
        {
            _pending?.Cancel();
            _pending?.Dispose();
            _pending = pending;
        }

        _ = WarmUpLaterAsync(messages, request.Options, elapsed < _delay ? _delay - elapsed : TimeSpan.Zero, pending.Token);
    }

    // Only Cancel disposes the token source, under the lock; this method uses just its token.
    private async Task WarmUpLaterAsync(List<ChatMessage> messages, ChatOptions? options, TimeSpan wait, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(wait, time, cancellationToken);
            Record(await warmer.WarmUpAsync(messages, options, cancellationToken));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _missed = true;
            LogFailed(logger, exception);
        }
        catch (Exception)
        {
            // Cancelled: the conversation resumed first. The warm-up task is fire-and-forget, so nothing may escape.
        }
    }

    private void Record(UsageDetails? details)
    {
        if (details is null)
        {
            return;
        }

        var input = details.InputTokenCount ?? 0;
        var cached = details.CachedInputTokenCount ?? 0;
        var missed = cached * 2 < input;
        _missed |= missed;
        usage.Add(input, details.OutputTokenCount ?? 0, cached);
        LogWarmedUp(logger, input, cached);
        if (missed)
        {
            LogMissed(logger);
        }
    }

    private static bool HasContent(ChatMessage message) =>
        message.Contents.Any(content => content is FunctionCallContent || content is TextContent { Text: var text } && !string.IsNullOrWhiteSpace(text));

    // Stub after the model reply: a "pending" result per call (Anthropic rejects the request without it) and a text line.
    private static ChatMessage Placeholder(IEnumerable<ChatMessage> answer) => new(ChatRole.User,
    [
        .. answer.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Select(call => new FunctionResultContent(call.CallId, PendingResult)),
        new TextContent(PlaceholderText),
    ]);

    private readonly record struct Request(List<ChatMessage> History, ChatOptions? Options, DateTimeOffset Started);

    [LoggerMessage(Level = LogLevel.Information, Message = "Prompt cache warmed up for an hour: input {InputTokens} tokens, from cache {CachedTokens}")]
    private static partial void LogWarmedUp(ILogger logger, long inputTokens, long cachedTokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt cache warm-up missed the cache: no more warm-ups for this conversation")]
    private static partial void LogMissed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt cache warm-up failed: no more warm-ups for this conversation")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
