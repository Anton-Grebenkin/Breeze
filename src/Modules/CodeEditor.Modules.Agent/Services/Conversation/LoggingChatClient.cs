using System.ClientModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Logs every model request: model, duration, time to first chunk, tokens, finish reason and tool call count; service
/// errors with the stack. Message and response text is not logged: it contains user code. One agent turn makes several
/// requests (a step per tool call round). <see cref="PromptCacheMonitor"/> flags prefix changes and cache misses, and
/// the actual input corrects the context estimate (<see cref="ContextMeter"/>).
/// </summary>
internal sealed partial class LoggingChatClient(IChatClient inner, string model, PromptCacheMonitor cache, ContextMeter meter, ILogger logger) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var call = new CallStats(model, messages);
        cache.BeforeRequest(options);
        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            call.Observe(new ChatResponseUpdate { ModelId = response.ModelId, FinishReason = response.FinishReason, Contents = [.. response.Messages.SelectMany(message => message.Contents), .. Usage(response.Usage)] });
            Complete(call);
            return response;
        }
        catch (Exception exception) when (Report(exception, call, cancellationToken))
        {
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var call = new CallStats(model, messages);
        cache.BeforeRequest(options);
        await using var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            try
            {
                if (!await updates.MoveNextAsync())
                {
                    break;
                }
            }
            catch (Exception exception) when (Report(exception, call, cancellationToken))
            {
                throw;
            }

            call.Observe(updates.Current);
            yield return updates.Current;
        }

        Complete(call);
    }

    private static IEnumerable<AIContent> Usage(UsageDetails? usage) => usage is null ? [] : [new UsageContent(usage)];

    private void Complete(CallStats call)
    {
        LogCompleted(logger, call.Model, call.Messages, call.ElapsedMs, call.FirstChunkMs, call.InputTokens, call.OutputTokens, call.CachedTokens, call.ReasoningTokens, call.ReasoningCharacters, call.ToolCalls, call.FinishReason);
        cache.AfterResponse(call.InputTokens, call.CachedTokens);
        meter.Observed(call.InputTokens ?? 0);
    }

    // Used as an exception filter: logs without catching, so the original stack is kept.
    private bool Report(Exception exception, CallStats call, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            LogCanceled(logger, call.Model, call.ElapsedMs);
        }
        else
        {
            var body = exception is ClientResultException result ? ServiceErrorText.Body(result) : null;
            LogFailed(logger, exception, call.Model, call.ElapsedMs, body is null ? exception.GetType().Name : $"{exception.GetType().Name}, body: {body}");
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, EventName = "ModelCall",
        Message = "Model {Model}: {Messages} messages, response in {ElapsedMs} ms (first chunk {FirstChunkMs} ms), tokens: input {InputTokens}, output {OutputTokens}, cached {CachedTokens}, reasoning {ReasoningTokens}; reasoning text {ReasoningCharacters} chars; tool calls {ToolCalls}, finish reason {FinishReason}")]
    private static partial void LogCompleted(ILogger logger, string model, int messages, long elapsedMs, long? firstChunkMs, long? inputTokens, long? outputTokens, long? cachedTokens, long? reasoningTokens, int reasoningCharacters, int toolCalls, string? finishReason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Model {Model}: request stopped after {ElapsedMs} ms")]
    private static partial void LogCanceled(ILogger logger, string model, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model {Model}: request failed after {ElapsedMs} ms ({Error})")]
    private static partial void LogFailed(ILogger logger, Exception exception, string model, long elapsedMs, string error);

    /// <summary>What is known about the request as chunks arrive.</summary>
    private sealed class CallStats(string configuredModel, IEnumerable<ChatMessage> messages)
    {
        private readonly long _started = Stopwatch.GetTimestamp();

        public string Model { get; private set; } = configuredModel;

        public int Messages { get; } = messages.TryGetNonEnumeratedCount(out var count) ? count : messages.Count();

        public long ElapsedMs => (long)Stopwatch.GetElapsedTime(_started).TotalMilliseconds;

        public long? FirstChunkMs { get; private set; }

        public long? InputTokens { get; private set; }

        public long? OutputTokens { get; private set; }

        public long? CachedTokens { get; private set; }

        /// <summary>Reasoning tokens as reported by the service; <c>null</c> if not reported.</summary>
        public long? ReasoningTokens { get; private set; }

        /// <summary>Reasoning text that reached the feed; 0 with a reasoning model means the proxy withholds it.</summary>
        public int ReasoningCharacters { get; private set; }

        public int ToolCalls { get; private set; }

        public string? FinishReason { get; private set; }

        public void Observe(ChatResponseUpdate update)
        {
            FirstChunkMs ??= ElapsedMs;
            Model = update.ModelId ?? Model;
            FinishReason = update.FinishReason?.Value ?? FinishReason;
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent:
                        ToolCalls++;
                        break;
                    case TextReasoningContent reasoning:
                        ReasoningCharacters += reasoning.Text?.Length ?? 0;
                        break;
                    case UsageContent usage:
                        InputTokens = usage.Details.InputTokenCount ?? InputTokens;
                        OutputTokens = usage.Details.OutputTokenCount ?? OutputTokens;
                        CachedTokens = usage.Details.CachedInputTokenCount ?? CachedTokens;
                        ReasoningTokens = usage.Details.ReasoningTokenCount ?? ReasoningTokens;
                        break;
                }
            }
        }
    }
}
