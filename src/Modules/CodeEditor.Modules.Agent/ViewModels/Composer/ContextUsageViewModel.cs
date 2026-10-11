using System.Globalization;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// Context window fill: a percentage ring by the input box with details on click (conversation size, chat spend, cache
/// hits). When the service reports no usage, it is estimated from the text (≈). While a request is on its way the ring
/// shows its estimated size (≈, <see cref="Preview"/>): after a large tool result or a compaction it moves at once,
/// not after the response.
/// </summary>
public sealed partial class ContextUsageViewModel : ObservableObject, IDisposable
{
    /// <summary>Rough estimate when the service reports no usage.</summary>
    public const int CharactersPerToken = 4;

    /// <summary>From this fill the ring warns that it is time to start a new chat.</summary>
    public const double WarningFraction = 0.8;

    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly IDisposable? _subscription;
    private readonly ContextMeter? _meter;
    private readonly IUiDispatcher? _dispatcher;
    private ContextUsage? _beforeLast;

    /// <param name="meter">Estimates each request before it is sent; without it the ring moves only with responses.</param>
    public ContextUsageViewModel(IOptionsMonitor<AgentOptions> options, ContextMeter? meter = null, IUiDispatcher? dispatcher = null)
    {
        _options = options;
        _subscription = options.OnChange(_ => OnPropertyChanged(string.Empty));
        if (meter is not null && dispatcher is not null)
        {
            _meter = meter;
            _dispatcher = dispatcher;
            _meter.Estimated += OnEstimated;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fraction), nameof(PercentText), nameof(IsNearlyFull), nameof(Summary), nameof(Spent), nameof(Hint))]
    public partial ContextUsage Usage { get; private set; } = ContextUsage.Empty;

    /// <summary>Estimated size of the request being sent; <c>null</c> once its usage arrives.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fraction), nameof(PercentText), nameof(IsNearlyFull), nameof(Summary), nameof(Hint))]
    public partial long? PendingTokens { get; private set; }

    [ObservableProperty]
    public partial bool IsDetailsOpen { get; set; }

    /// <summary>
    /// The chat's context limit: the model window capped by <c>agent.contextLimit</c>, where compaction starts.
    /// </summary>
    public int ContextWindow => ContextCompaction.ContextLimit(_options.CurrentValue);

    /// <summary>The conversation size the ring shows: the request on its way, else the last one.</summary>
    public long ContextTokens => PendingTokens ?? Usage.ContextTokens;

    public double Fraction => Math.Clamp(ContextTokens / (double)ContextWindow, 0, 1);

    public string PercentText => Approx + TokenText.Percent(Fraction);

    public bool IsNearlyFull => Fraction >= WarningFraction;

    public string Summary => Format(
        Strings.ContextSummary, Approx, TokenText.Format(ContextTokens), TokenText.Format(ContextWindow), TokenText.Percent(Fraction));

    public string Spent => Usage.Requests == 0
        ? Strings.ContextNoRequests
        : Format(Strings.ContextSpent, Usage.Requests, Approx, TokenText.Format(Usage.TotalInputTokens), TokenText.Format(Usage.TotalOutputTokens))
          + (Usage.TotalCachedInputTokens > 0 ? Format(Strings.ContextSpentCached, TokenText.Percent(CacheShare), TokenText.Format(Usage.CachedInputTokens)) : ".");

    /// <summary>
    /// Share of the chat's input served from the provider cache; closer to 100 % means a cheaper chat (ADR 0012).
    /// </summary>
    public double CacheShare => Usage.TotalInputTokens == 0 ? 0 : Math.Clamp(Usage.TotalCachedInputTokens / (double)Usage.TotalInputTokens, 0, 1);

    public string Hint => IsNearlyFull ? Strings.ContextNearlyFull : LimitHint;

    // When the chat limit is below the model window, explain what happens next and where to change it.
    private string LimitHint => ModelCatalog.ContextWindowFor(_options.CurrentValue) is var window && window > ContextWindow
        ? Format(Strings.ContextLimitHint, TokenText.Format(ContextWindow), TokenText.Format(window))
        : Strings.ContextWindowHint;

    private string Approx => Usage.IsEstimated || PendingTokens is not null ? "≈" : string.Empty;

    /// <summary>Adds the usage of one model request; an agent turn makes one per step.</summary>
    /// <param name="revisesLast">A later report of the same request (a running total): it replaces the last one.</param>
    public void Add(long? inputTokens, long? outputTokens, long? cachedInputTokens, bool revisesLast = false)
    {
        var input = inputTokens ?? 0;
        var output = outputTokens ?? 0;
        var previous = revisesLast && _beforeLast is { } before ? before : Usage.IsEstimated ? ContextUsage.Empty : Usage;
        _beforeLast = previous;
        PendingTokens = null;
        Usage = new ContextUsage(input + output, previous.TotalInputTokens + input, previous.TotalOutputTokens + output)
        {
            CachedInputTokens = cachedInputTokens ?? 0,
            TotalCachedInputTokens = previous.TotalCachedInputTokens + (cachedInputTokens ?? 0),
            Requests = previous.Requests + 1,
        };
    }

    /// <summary>A side request with its own history (reviewer, cache warm-up): adds to spend, not context fill.</summary>
    public void AddAuxiliary(long inputTokens, long outputTokens, long cachedInputTokens = 0)
    {
        Usage = WithAuxiliary(Usage, inputTokens, outputTokens, cachedInputTokens);
        _beforeLast = _beforeLast is { } before ? WithAuxiliary(before, inputTokens, outputTokens, cachedInputTokens) : null;
    }

    /// <summary>Estimates usage from the conversation text length when the service reports none.</summary>
    public void Estimate(long characters, long answerCharacters)
    {
        var tokens = characters / CharactersPerToken;
        Usage = new ContextUsage(tokens, Usage.TotalInputTokens + tokens, Usage.TotalOutputTokens + answerCharacters / CharactersPerToken)
        {
            Requests = Usage.Requests + 1,
            IsEstimated = true,
        };
        PendingTokens = null;
    }

    /// <summary>A request is about to be sent with about this many tokens.</summary>
    public void Preview(long tokens) => PendingTokens = tokens;

    public void Restore(ContextUsage? usage)
    {
        Usage = usage ?? ContextUsage.Empty;
        _beforeLast = null;
        PendingTokens = null;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        if (_meter is not null)
        {
            _meter.Estimated -= OnEstimated;
        }
    }

    private void OnEstimated(object? sender, long tokens) => _dispatcher!.Post(() => Preview(tokens));

    private static ContextUsage WithAuxiliary(ContextUsage usage, long inputTokens, long outputTokens, long cachedInputTokens) => usage with
    {
        TotalInputTokens = usage.TotalInputTokens + inputTokens,
        TotalOutputTokens = usage.TotalOutputTokens + outputTokens,
        TotalCachedInputTokens = usage.TotalCachedInputTokens + cachedInputTokens,
        Requests = usage.Requests + 1,
    };

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    [RelayCommand]
    private void ShowDetails() => IsDetailsOpen = true;
}
