using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>Notices under a turn's result (limit, plan, a change to apply) and a usage estimate when the service sent none.</summary>
internal static class TurnEnding
{
    public static string PlanReadyNotice => Strings.PlanReady;

    public static string ApplyOfferNotice => Strings.ApplyOffer;

    /// <summary>
    /// Request limit → Continue; a plan in Plan mode → hand-off to Agent mode; an Ask answer with a change → "Apply in
    /// Agent mode", so the user does not switch the mode and retype the request.
    /// </summary>
    public static void AddNotice(ObservableCollection<ChatMessageViewModel> messages, AgentTurn turn, AgentMode mode)
    {
        if (turn.ReachedStepLimit)
        {
            messages.Add(new ChatMessageViewModel(ChatMessageKind.Notice, string.Format(CultureInfo.CurrentCulture, Strings.StepLimitNotice, turn.StepLimit)));
        }
        else if (messages.Count == 0 || messages[^1].Kind != ChatMessageKind.Assistant)
        {
            return;
        }
        else if (mode == AgentMode.Plan)
        {
            messages.Add(new ChatMessageViewModel(ChatMessageKind.Handoff, PlanReadyNotice));
        }
        else if (mode == AgentMode.Ask && (turn.ChangeRefused || PointsToAgentMode(messages[^1].Text)))
        {
            messages.Add(new ChatMessageViewModel(ChatMessageKind.ApplyOffer, ApplyOfferNotice));
        }
    }

    // The Ask prompt tells the model to say that Agent mode can apply a change. How that sounds depends on the
    // language, so the pattern is a resource: English everywhere, plus the wording of the interface language.
    private static bool PointsToAgentMode(string answer)
    {
        try
        {
            return Regex.IsMatch(answer, Strings.AgentModeMention, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (Exception exception) when (exception is RegexMatchTimeoutException or ArgumentException)
        {
            return false;
        }
    }

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Estimates usage from the text when the service sent none (not all compatible APIs do).</summary>
    public static void EstimateUsage(ObservableCollection<ChatMessageViewModel> messages, AgentTurn turn, ContextUsageViewModel usage)
    {
        // A notice may follow the answer; estimate the answer itself.
        if (turn.UsageReported || messages.LastOrDefault(static message => !IsNotice(message.Kind)) is not { Kind: ChatMessageKind.Assistant } answer)
        {
            return;
        }

        usage.Estimate(messages.Sum(message => (long)message.Text.Length), answer.Text.Length);
    }

    private static bool IsNotice(ChatMessageKind kind) => kind is ChatMessageKind.Notice or ChatMessageKind.Handoff or ChatMessageKind.ApplyOffer;
}
