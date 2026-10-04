using CodeEditor.Modules.Agent.Resources;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>
/// The checks node of the turn graph (ADR 0013): the model answered without approval requests. First come images the
/// agent opened (ADR 0030), then a queued user message (ADR 0026); both reach the model before any check. An answer cut
/// by the length limit is continued (up to twice); then the harness decides by external signals (<see cref="TurnChecks"/>:
/// build, tests, open plan items, Deep mode review) whether to send the model back with a hint or end the turn.
/// </summary>
public sealed class ChecksExecutor(TurnChecks checks, TurnBudget budget, DeepSupervisor deep, UserMessageQueue queue, ToolImages images, CancellationToken turn) : Executor<ModelOutcome>(NodeId)
{
    public const string NodeId = "checks";

    /// <summary>How many times in a row to continue an answer cut by the length limit.</summary>
    public const int MaxLengthContinuations = 2;

    private int _continuations;

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        base.ConfigureProtocol(protocolBuilder).SendsMessage<TurnInput>().YieldsOutput<TurnResult>();

    public override async ValueTask HandleAsync(ModelOutcome outcome, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(context);
        // The graph does not pass the turn's cancellation to nodes; the review and advisor must stop with the turn.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, turn);
        cancellationToken = linked.Token;
        if (images.Take() is { } shown)
        {
            await context.SendMessageAsync(new TurnInput(shown), cancellationToken: cancellationToken);
            return;
        }

        if (queue.TryDequeue(out var queued))
        {
            await context.AddEventAsync(new UserMessageDeliveredEvent(queued), cancellationToken);
            await context.SendMessageAsync(new TurnInput(queued.Message), cancellationToken: cancellationToken);
            return;
        }

        if (outcome.FinishReason == ChatFinishReason.Length)
        {
            if (_continuations++ < MaxLengthContinuations && !budget.IsExhausted)
            {
                await context.SendMessageAsync(new TurnInput(new ChatMessage(ChatRole.User, PromptSections.EditorNote(Strings.ContinueAfterLength)), Continues: true), cancellationToken: cancellationToken);
                return;
            }

            // Continuations did not help (often reasoning eats the whole limit): tell the user instead of failing silently.
            await context.AddEventAsync(new TurnNoticeEvent(ChatMessageKind.Status, Strings.LengthLimitNotice), cancellationToken);
        }

        var check = await checks.NextAsync(outcome.Answer, truncated: outcome.FinishReason == ChatFinishReason.Length, cancellationToken);
        foreach (var notice in deep.TakeNotices())
        {
            await context.AddEventAsync(new TurnNoticeEvent(ChatMessageKind.Progress, notice), cancellationToken);
        }

        if (check is not null)
        {
            await context.AddEventAsync(new TurnNoticeEvent(ChatMessageKind.Status, check.Status), cancellationToken);
        }

        if (check?.Prompt is { } prompt)
        {
            await context.SendMessageAsync(new TurnInput(new ChatMessage(ChatRole.User, PromptSections.EditorNote(prompt)), Revises: true), cancellationToken: cancellationToken);
            return;
        }

        await context.YieldOutputAsync(new TurnResult(budget.IsExhausted), cancellationToken);
    }
}
