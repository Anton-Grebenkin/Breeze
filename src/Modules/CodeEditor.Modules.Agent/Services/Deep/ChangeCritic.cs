using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// Independent review before the Deep mode summary (ADR 0012): the advisor model in a clean context gets the task, the
/// chat's diff and the agent's report, plus read tools to check claims against the code. It does not see the agent's
/// reasoning and does not inherit its assumptions. A review failure does not break the turn: the review is skipped.
/// </summary>
public sealed partial class ChangeCritic(
    SubagentRunner runner,
    ReadOnlyToolSet readTools,
    IOptionsMonitor<AgentOptions> options,
    ChatChanges changes,
    ILogger<ChangeCritic> logger)
{
    public const int MaxRequests = 10;

    /// <summary>A longer diff is cut; the reviewer reads the rest with tools.</summary>
    public const int MaxDiffCharacters = 60_000;

    /// <returns>The review, or <c>null</c> if there are no edits or the reviewer did not answer.</returns>
    public async Task<ReviewResult?> ReviewAsync(string task, string report, CancellationToken cancellationToken)
    {
        var diff = ChatChanges.Diff(await changes.CollectAsync());
        if (diff.Length == 0)
        {
            return null;
        }

        var agent = options.CurrentValue;
        var run = new SubagentRunner.Run(Advisor.Settings(agent), AdvisorPrompts.Review, agent.AdvisorReasoningEffort, MaxRequests);
        try
        {
            var answer = await runner.RunAsync(run, AdvisorPrompts.ReviewRequest(task, Limit(diff), report), readTools.Tools, cancellationToken);
            var result = ReviewResult.Parse(answer);
            LogReviewed(logger, run.Model.Model, result.Blocking.Count, result.Advisory.Count);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception, run.Model.Model);
            return null;
        }
    }

    private static string Limit(string diff) =>
        diff.Length <= MaxDiffCharacters ? diff : string.Concat(diff.AsSpan(0, MaxDiffCharacters), "\n…");

    [LoggerMessage(Level = LogLevel.Information, Message = "Review on {Model}: {Blocking} blocking, {Advisory} advisory findings")]
    private static partial void LogReviewed(ILogger logger, string model, int blocking, int advisory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reviewer {Model} is unavailable, the review is skipped")]
    private static partial void LogFailed(ILogger logger, Exception exception, string model);
}
