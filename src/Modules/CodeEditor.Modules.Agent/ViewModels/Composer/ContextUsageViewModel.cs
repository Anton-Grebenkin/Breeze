using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// Context window fill: a percentage ring by the input box with details on click (conversation size, chat spend, cache
/// hits). When the service reports no usage, it is estimated from the text (≈).
/// </summary>
public sealed partial class ContextUsageViewModel : ObservableObject, IDisposable
{
    /// <summary>Rough estimate when the service reports no usage.</summary>
    public const int CharactersPerToken = 4;

    /// <summary>From this fill the ring warns that it is time to start a new chat.</summary>
    public const double WarningFraction = 0.8;

    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly IDisposable? _subscription;

    public ContextUsageViewModel(IOptionsMonitor<AgentOptions> options)
    {
        _options = options;
        _subscription = options.OnChange(_ => OnPropertyChanged(string.Empty));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fraction), nameof(PercentText), nameof(IsNearlyFull), nameof(Summary), nameof(Spent), nameof(Hint))]
    public partial ContextUsage Usage { get; private set; } = ContextUsage.Empty;

    [ObservableProperty]
    public partial bool IsDetailsOpen { get; set; }

    /// <summary>
    /// The chat's context limit: the model window capped by <c>agent.contextLimit</c>, where compaction starts.
    /// </summary>
    public int ContextWindow => ContextCompaction.ContextLimit(_options.CurrentValue);

    public double Fraction => Math.Clamp(Usage.ContextTokens / (double)ContextWindow, 0, 1);

    public string PercentText => Approx + TokenText.Percent(Fraction);

    public bool IsNearlyFull => Fraction >= WarningFraction;

    public string Summary => Format(
        Strings.ContextSummary, Approx, TokenText.Format(Usage.ContextTokens), TokenText.Format(ContextWindow), TokenText.Percent(Fraction));

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

    private string Approx => Usage.IsEstimated ? "≈" : string.Empty;

    /// <summary>Adds the usage of one model request; an agent turn makes one per step.</summary>
    public void Add(long? inputTokens, long? outputTokens, long? cachedInputTokens)
    {
        var input = inputTokens ?? 0;
        var output = outputTokens ?? 0;
        var previous = Usage.IsEstimated ? ContextUsage.Empty : Usage;
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
        Usage = Usage with
        {
            TotalInputTokens = Usage.TotalInputTokens + inputTokens,
            TotalOutputTokens = Usage.TotalOutputTokens + outputTokens,
            TotalCachedInputTokens = Usage.TotalCachedInputTokens + cachedInputTokens,
            Requests = Usage.Requests + 1,
        };
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
    }

    public void Restore(ContextUsage? usage)
    {
        Usage = usage ?? ContextUsage.Empty;
    }

    public void Dispose() => _subscription?.Dispose();

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    [RelayCommand]
    private void ShowDetails() => IsDetailsOpen = true;
}
