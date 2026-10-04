using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Compaction summary in the same request the model saw (ADR 0023): system prompt, tools, options and the whole
/// history as is, with the compaction request last (<see cref="CompactionPrompts.SummaryRequest"/>). The request prefix
/// is read from the cache, about 10% of the input price instead of a full separate request. The summary replaces the
/// oldest groups until the rest is below the target; recent groups stay as is. The oldest groups are replaced by the
/// verbatim user requests and the summary with the exact harness state and full history path
/// (<see cref="CompactedHistory"/>). If the conversation request is not known yet (first request of an opened chat) or
/// the model did not answer with a summary, the helper model writes it in a separate request.
/// </summary>
/// <param name="saveTranscript">Saves the compacted part of the history to a file; returns a line about it or <c>null</c>.</param>
internal sealed partial class ConversationSummaryStrategy(
    CompactionModels models,
    Func<string> state,
    Func<IReadOnlyList<ChatMessage>, string?> saveTranscript,
    CompactionTrigger trigger,
    CompactionTrigger target) : CompactionStrategy(trigger, target)
{
    /// <summary>Recent groups kept as is, so the model continues where it was.</summary>
    public const int MinimumPreservedGroups = 4;

    protected override async ValueTask<bool> CompactCoreAsync(CompactionMessageIndex index, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(logger);

        // Take the conversation request before excluding groups: that is how the model saw it and how it is cached.
        List<ChatMessage> history = [.. index.GetIncludedMessages()];
        var (groups, position) = Exclude(index);
        if (groups.Count == 0)
        {
            return false;
        }

        List<ChatMessage> summarized = [.. groups.SelectMany(static group => group.Messages)];
        var summary = await SummarizeAsync(history, summarized, logger, cancellationToken);
        if (summary is null)
        {
            Restore(groups);
            return false;
        }

        var requests = CompactedHistory.RequestsMessage(CompactedHistory.Requests(summarized));
        var note = CompactedHistory.SummaryMessage(summary, state(), saveTranscript(summarized));
        index.InsertGroup(position, CompactionGroupKind.Summary, requests is null ? [note] : [requests, note]);
        return true;
    }

    // Oldest groups, except system and recent ones, until the rest is below the target.
    private (List<CompactionMessageGroup> Groups, int Position) Exclude(CompactionMessageIndex index)
    {
        var candidates = index.Groups.Count(static group => !group.IsExcluded && group.Kind != CompactionGroupKind.System) - MinimumPreservedGroups;
        var excluded = new List<CompactionMessageGroup>();
        var position = -1;
        for (var i = 0; i < index.Groups.Count && excluded.Count < candidates; i++)
        {
            var group = index.Groups[i];
            if (group.IsExcluded || group.Kind == CompactionGroupKind.System)
            {
                continue;
            }

            position = position < 0 ? i : position;
            group.IsExcluded = true;
            group.ExcludeReason = "Summarized by " + nameof(ConversationSummaryStrategy);
            excluded.Add(group);
            if (Target(index))
            {
                break;
            }
        }

        return (excluded, position);
    }

    private static void Restore(List<CompactionMessageGroup> groups)
    {
        foreach (var group in groups)
        {
            group.IsExcluded = false;
            group.ExcludeReason = null;
        }
    }

    private async Task<string?> SummarizeAsync(List<ChatMessage> history, List<ChatMessage> summarized, ILogger logger, CancellationToken cancellationToken)
    {
        if (models.ConversationOptions() is { } options
            && await RequestAsync(models.Conversation, [.. history, new ChatMessage(ChatRole.User, CompactionPrompts.SummaryRequest)], options.Clone(), logger, cancellationToken) is { } summary)
        {
            return summary;
        }

        LogHelperSummary(logger);
        return await RequestAsync(models.Helper, [new ChatMessage(ChatRole.System, CompactionPrompts.Summary), .. summarized], options: null, logger, cancellationToken);
    }

    // The summary is answer text without calls: a model that reached for tools did not write a summary.
    private static async Task<string?> RequestAsync(IChatClient client, List<ChatMessage> messages, ChatOptions? options, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);
            LogSummary(logger, response.Usage?.InputTokenCount, response.Usage?.CachedInputTokenCount);
            var calls = response.Messages.SelectMany(static message => message.Contents).Any(static content => content is FunctionCallContent);
            return calls || string.IsNullOrWhiteSpace(response.Text) ? null : response.Text;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Context summary written: input {InputTokens} tokens, from cache {CachedTokens}")]
    private static partial void LogSummary(ILogger logger, long? inputTokens, long? cachedTokens);

    [LoggerMessage(Level = LogLevel.Information, Message = "Context summary by the helper model: the conversation request is unknown or gave no summary")]
    private static partial void LogHelperSummary(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Context summary request failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
