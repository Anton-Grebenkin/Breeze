using System.Collections.Concurrent;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Context compaction before every model request, including inside the tool loop (ADR 0009, 0012, 0023).
/// Thresholds are relative to the input budget: the model window minus the answer length and a prompt reserve, capped
/// by the chat limit (<c>agent.contextLimit</c>, 200K). Model quality drops with input size long before the window
/// ends, and every request pays for the whole context. Stages:
/// <list type="bullet">
/// <item>from 80%, the early conversation is replaced with a summary written by the conversation model in the same
/// request, from the cache (<see cref="ConversationSummaryStrategy"/>). The harness appends verbatim user requests,
/// changed files, the plan and the full history path. The summary runs first: it repeats the request as cached;</item>
/// <item>from 50%, old tool results fold into "call → start of result" lines down to 30%: as good as a summary at
/// half the cost;</item>
/// <item>from 95%, the oldest groups are dropped.</item>
/// </list>
/// Every compaction breaks the cache, so each stage compacts with a wide margin (hysteresis). Tokens are corrected by
/// the model's reported usage (<see cref="ContextMeter"/>). State lives in the session: compacted stays compacted, and
/// the request prefix stays stable for the cache.
/// </summary>
public sealed class ContextCompaction(
    AgentFileState fileState,
    TodoList todos,
    IWorkspace workspace,
    ChatStartContext start,
    HelperUsage usage,
    AgentOutputStore outputs,
    ILoggerFactory loggerFactory)
{
    /// <summary>Name of the file with the summarized history (in <c>.breeze/agent/outputs/</c>).</summary>
    public const string HistoryFileName = "history";

    public const string StateKey = "codeeditor.compaction";

    /// <summary>Reserve for the system prompt and tool schemas: not part of the history, but they take window space.</summary>
    public const int ReservedTokens = 12_000;

    /// <summary>Window space left for the answer when the answer length is not set.</summary>
    public const int DefaultOutputTokens = 16_000;

    /// <summary>Chat context limit when <c>agent.contextLimit</c> is not set.</summary>
    public const int DefaultContextLimit = 200_000;

    public const int MinimumInputBudget = 4_000;

    public static string SummarizedNotice => Strings.ContextSummarized;
    public static string TruncatedNotice => Strings.ContextTruncated;

    private readonly ConcurrentQueue<string> _notices = new();

    /// <summary>Chat context limit: the model window, capped by <c>agent.contextLimit</c>.</summary>
    public static int ContextLimit(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Math.Min(ModelCatalog.ContextWindowFor(options), options.ContextLimit is > 0 and var limit ? limit : DefaultContextLimit);
    }

    /// <summary>
    /// Input budget in tokens: the model window minus the answer length and reserve, capped by the chat limit. The
    /// answer length is subtracted only from the window: the chat limit is not the window edge, so the answer still fits.
    /// </summary>
    public static int InputBudget(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var byWindow = ModelCatalog.ContextWindowFor(options) - (options.MaxOutputTokens ?? DefaultOutputTokens) - ReservedTokens;
        var limit = options.ContextLimit is > 0 and var configured ? configured : DefaultContextLimit;
        return Math.Max(Math.Min(byWindow, limit), MinimumInputBudget);
    }

    /// <summary>Context provider for the chat client pipeline: compacts messages before every request.</summary>
    /// <param name="models">Conversation and helper models for the summary; both count toward the chat's usage.</param>
    /// <param name="meter">Corrects the token estimate by the real input, so thresholds are in model tokens.</param>
    public AIContextProvider CreateProvider(AgentOptions options, CompactionModels models, ContextMeter meter)
    {
        ArgumentNullException.ThrowIfNull(models);
        var budget = InputBudget(options);
        CompactionTrigger Exceeds(double share) => index => meter.Tokens(ContextEstimate.Of(index)) > (int)(budget * share);
        CompactionTrigger Below(double share) => index => meter.Tokens(ContextEstimate.Of(index)) < (int)(budget * share);
        var counted = models with
        {
            Conversation = new UsageCountingChatClient(models.Conversation, usage),
            Helper = new UsageCountingChatClient(models.Helper, usage),
        };
        var summary = new ConversationSummaryStrategy(counted, HarnessState, SaveHistory, Exceeds(0.8), Below(0.5));
        var tools = new ToolResultCompactionStrategy(Exceeds(0.5), minimumPreservedGroups: 6, target: Below(0.3))
        {
            ToolCallFormatter = CompactionPrompts.FormatToolGroup,
        };
        var truncation = new TruncationCompactionStrategy(Exceeds(0.95), minimumPreservedGroups: 2, target: Below(0.7));
        var pipeline = new PipelineCompactionStrategy(
        [
            new NotifyingCompactionStrategy(summary, OnSummarized),
            new NotifyingCompactionStrategy(tools, fileState.ForgetWindows),
            new NotifyingCompactionStrategy(truncation, () => _notices.Enqueue(TruncatedNotice)),
            new MeteringCompactionStrategy(meter),
        ]);
        return new CompactionProvider(pipeline, StateKey, loggerFactory);
    }

    /// <summary>What was compacted since the last call, as feed lines.</summary>
    public IReadOnlyList<string> TakeNotices()
    {
        var notices = new List<string>();
        while (_notices.TryDequeue(out var notice))
        {
            notices.Add(notice);
        }

        return notices;
    }

    /// <summary>
    /// What the harness knows exactly, appended to the summary: files changed in the chat, the plan, the folder
    /// snapshot and the memory index (the first chat message holding them is already compacted).
    /// </summary>
    public string HarnessState()
    {
        var changed = fileState.Changes
            .Select(change => workspace.RelativePath(change.Key) + (change.Value is null ? " (created)" : string.Empty))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var parts = new List<string>();
        if (changed.Count > 0)
        {
            parts.Add(CompactionPrompts.ChangedFilesLine + string.Join(", ", changed) + ".");
        }

        if (!todos.Items.IsEmpty)
        {
            parts.Add(CompactionPrompts.PlanLine + "\n" + todos.Render());
        }

        parts.AddRange(start.Blocks());
        return parts.Count == 0 ? string.Empty : CompactionPrompts.StateHeader + "\n" + string.Join('\n', parts);
    }

    // The summarized history is saved to a file so the model can read exact errors and code from it.
    private string? SaveHistory(IReadOnlyList<ChatMessage> messages)
    {
        var text = HistoryTranscript.Render(messages);
        return outputs.Save(text, HistoryFileName) is { } path
            ? string.Format(CultureInfo.InvariantCulture, CompactionPrompts.TranscriptLine, path, text.AsSpan().Count('\n'))
            : null;
    }

    // Read file contents are gone from the context: editing from memory is risky, so files must be read again.
    private void OnSummarized()
    {
        fileState.ForgetReads();
        _notices.Enqueue(SummarizedNotice);
    }
}
