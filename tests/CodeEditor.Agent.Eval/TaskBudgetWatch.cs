using CodeEditor.Modules.Agent.Services.Context;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Per-task cost cap: every <see cref="Period"/> it prices the chat usage and stops the turn once the cap is exceeded,
/// so a model looping on an expensive task can't eat the whole run's budget. Does nothing without a cap or a price.
/// </summary>
internal sealed class TaskBudgetWatch : IDisposable
{
    public const string ExceededError = "превышен предел расхода на задачу";

    public static readonly TimeSpan Period = TimeSpan.FromSeconds(2);

    private readonly Timer? _timer;
    private int _exceeded;

    /// <param name="usage">Current chat usage; read on the timer thread.</param>
    /// <param name="stop">Stops the turn; called at most once.</param>
    public TaskBudgetWatch(string model, string service, decimal? maxCost, Func<ContextUsage> usage, Action stop)
    {
        if (maxCost is not { } limit || ModelPrice.For(model, service) is not { } price)
        {
            return;
        }

        _timer = new Timer(_ =>
        {
            var current = usage();
            if (price.Cost(current.TotalInputTokens, current.TotalCachedInputTokens, current.TotalOutputTokens) > limit
                && Interlocked.Exchange(ref _exceeded, 1) == 0)
            {
                stop();
            }
        }, null, Period, Period);
    }

    /// <summary>The turn was stopped by the cap.</summary>
    public bool Exceeded => Volatile.Read(ref _exceeded) == 1;

    public void Dispose() => _timer?.Dispose();
}
