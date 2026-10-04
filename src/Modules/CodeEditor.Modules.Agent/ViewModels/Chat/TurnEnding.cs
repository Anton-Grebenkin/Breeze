using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>Notices under a turn's result (limit, plan) and a usage estimate when the service sent none.</summary>
internal static class TurnEnding
{
    public static string PlanReadyNotice => Strings.PlanReady;

    private static string StepLimitNotice => string.Format(CultureInfo.CurrentCulture, Strings.StepLimitNotice, AgentModes.RequestLimit);

    /// <summary>Request limit → Continue; a plan in Plan mode → hand-off to Agent mode.</summary>
    public static void AddNotice(ObservableCollection<ChatMessageViewModel> messages, AgentTurn turn, AgentMode mode)
    {
        if (turn.ReachedStepLimit)
        {
            messages.Add(new ChatMessageViewModel(ChatMessageKind.Notice, StepLimitNotice));
        }
        else if (mode == AgentMode.Plan && messages.Count > 0 && messages[^1].Kind == ChatMessageKind.Assistant)
        {
            messages.Add(new ChatMessageViewModel(ChatMessageKind.Handoff, PlanReadyNotice));
        }
    }

    /// <summary>Estimates usage from the text when the service sent none (not all compatible APIs do).</summary>
    public static void EstimateUsage(ObservableCollection<ChatMessageViewModel> messages, AgentTurn turn, ContextUsageViewModel usage)
    {
        // A notice may follow the answer; estimate the answer itself.
        if (turn.UsageReported || messages.LastOrDefault(message => message.Kind != ChatMessageKind.Notice && message.Kind != ChatMessageKind.Handoff) is not { Kind: ChatMessageKind.Assistant } answer)
        {
            return;
        }

        usage.Estimate(messages.Sum(message => (long)message.Text.Length), answer.Text.Length);
    }
}
