using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Workflow;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// One agent turn: the <see cref="AgentTurnWorkflow"/> graph loops "model → approvals → model → checks" while
/// the feed (<see cref="TurnFeed"/>) handles its events on the UI thread. Approval cards are a graph port request:
/// the turn waits for all user decisions, then answers the port.
/// </summary>
/// <param name="acceptEdits">Auto-approve edits, file creation and moves (<see cref="AgentApprovals.Auto"/>).</param>
public sealed class AgentTurn(ObservableCollection<ChatMessageViewModel> messages, AgentTurnServices services, ISet<string> approvedTools, bool acceptEdits)
{
    private readonly TurnFeed _feed = new(messages, services);
    private ApprovalExecutor? _approvals;
    private ModelExecutor? _model;

    public int ToolCalls => _feed.ToolCalls;

    public int Approvals => _approvals?.Count ?? 0;

    /// <summary>The service reported token usage; otherwise the session estimates it from the text.</summary>
    public bool UsageReported => _feed.UsageReported;

    /// <summary>How many times in a row to continue an answer cut off by the length limit.</summary>
    public const int MaxLengthContinuations = ChecksExecutor.MaxLengthContinuations;

    public static string LengthLimitNotice => Strings.LengthLimitNotice;

    public static string ContinueAfterLength => Strings.ContinueAfterLength;

    /// <summary>The turn hit the mode's step limit, so the result is intermediate.</summary>
    public bool ReachedStepLimit { get; private set; }

    public async Task RunAsync(ChatMessage message, AgentMode mode, CancellationToken cancellationToken)
    {
        services.Budget.Begin(mode);
        services.Images.Clear();
        services.Checks.Begin(mode, message.Contents.OfType<TextContent>().FirstOrDefault()?.Text);
        services.Deep.Begin(mode);
        _approvals = new ApprovalExecutor(services.ApprovalCards, services.Deep, services.Log, approvedTools, acceptEdits, mode, cancellationToken);
        _model = new ModelExecutor(services.Conversation, cancellationToken);
        var workflow = AgentTurnWorkflow.Build(_model, _approvals, new ChecksExecutor(services.Checks, services.Budget, services.Deep, services.Queue, services.Images, cancellationToken));

        Exception? failure = null;
        var run = await InProcessExecution.RunStreamingAsync(workflow, new TurnInput(message), cancellationToken: cancellationToken);
        try
        {
            await foreach (var evt in run.WatchStreamAsync(cancellationToken))
            {
                failure ??= await HandleAsync(evt, run, cancellationToken);
            }
        }
        finally
        {
            // On stop the event stream ends at once but the model request aborts a bit later; wait for it so the next
            // turn does not overlap with this one. The answer in the feed is closed with a mark.
            if (cancellationToken.IsCancellationRequested)
            {
                await _model.RoundFinished;
            }

            _feed.FinishRound(stopped: cancellationToken.IsCancellationRequested);
        }

        // The graph does not throw: cancellation silently ends the stream and executor errors arrive as events.
        cancellationToken.ThrowIfCancellationRequested();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }

    /// <summary>Rejects unanswered cards when the turn stops while waiting for a decision.</summary>
    public void RejectPendingApprovals()
    {
        foreach (var message in messages.Where(message => message.Approval is { IsPending: true }))
        {
            message.Approval!.Resolve(approved: false);
        }
    }

    /// <returns>An executor error to rethrow after the stream ends; otherwise <c>null</c>.</returns>
    private async Task<Exception?> HandleAsync(WorkflowEvent evt, StreamingRun run, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case ModelRoundStartedEvent started:
                _feed.StartRound(started.Continues, started.Revises);
                break;
            case ModelUpdateEvent update:
                foreach (var content in update.Update.Contents)
                {
                    _feed.Apply(content);
                }

                break;
            case ModelRoundFinishedEvent:
                _feed.FinishRound(stopped: false);
                break;
            case TurnNoticeEvent notice:
                _feed.Notice(notice.Kind, notice.Text);
                break;
            case UserMessageDeliveredEvent delivered:
                messages.Add(ChatMessageViewModel.FromUser(delivered.Message));
                break;
            case RequestInfoEvent request when request.Request.TryGetDataAs<ApprovalPending>(out var pending):
                await DecideAsync(pending!, request, run, cancellationToken);
                break;
            case WorkflowOutputEvent output when output.Is<TurnResult>():
                ReachedStepLimit = output.As<TurnResult>()!.ReachedStepLimit;
                break;
            case WorkflowErrorEvent error:
                return Unwrap(error.Exception ?? new InvalidOperationException("Workflow failed without an exception."));
        }

        return null;
    }

    // Cards go to the feed; the port is answered once the user has decided on all of them.
    private async Task DecideAsync(ApprovalPending pending, RequestInfoEvent request, StreamingRun run, CancellationToken cancellationToken)
    {
        foreach (var card in pending.Cards)
        {
            messages.Add(new ChatMessageViewModel(ChatMessageKind.Approval) { Approval = card });
        }

        await Task.WhenAll(pending.Cards.Select(card => card.Decision)).WaitAsync(cancellationToken);
        await run.SendResponseAsync(request.Request.CreateResponse(new ApprovalDecided()));
    }

    // Executor handlers are invoked via reflection; surface the original error, as the session shows it.
    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner })
        {
            exception = inner;
        }

        return exception;
    }
}
