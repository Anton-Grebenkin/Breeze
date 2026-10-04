using System.Text;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>
/// The model node of the turn graph (ADR 0013): one round is a model request with its tool loop. Answer fragments go to
/// the feed as events, and the round outcome goes on through the graph: approval requests lead to the approvals node,
/// an answer without them to the checks. The graph does not pass the turn's cancellation to nodes, so the turn token is
/// linked here; on stop the host awaits <see cref="RoundFinished"/> so the next turn does not overlap this one's abort.
/// </summary>
public sealed class ModelExecutor(AgentConversation conversation, CancellationToken turn) : Executor<TurnInput>(NodeId)
{
    public const string NodeId = "model";

    private volatile TaskCompletionSource _round = Completed();

    /// <summary>The model round ended, normally or by cancellation; completed between rounds.</summary>
    public Task RoundFinished => _round.Task;

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        base.ConfigureProtocol(protocolBuilder).SendsMessage<ModelOutcome>();

    public override async ValueTask HandleAsync(TurnInput input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, turn);
        _round = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await RunRoundAsync(input, context, linked.Token);
        }
        finally
        {
            _round.TrySetResult();
        }
    }

    private async Task RunRoundAsync(TurnInput input, IWorkflowContext context, CancellationToken cancellationToken)
    {
        await context.AddEventAsync(new ModelRoundStartedEvent(input.Continues, input.Revises), cancellationToken);
        var requests = new List<ToolApprovalRequestContent>();
        ChatFinishReason? finishReason = null;
        var answer = new StringBuilder();
        await foreach (var update in conversation.SendAsync(input.Message, cancellationToken))
        {
            finishReason = update.FinishReason ?? finishReason;
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextContent text:
                        answer.Append(text.Text);
                        break;
                    case FunctionCallContent:
                        // Text before calls is work log, not the final answer.
                        answer.Clear();
                        break;
                    case ToolApprovalRequestContent request:
                        requests.Add(request);
                        break;
                }
            }

            await context.AddEventAsync(new ModelUpdateEvent(update), cancellationToken);
        }

        await context.AddEventAsync(new ModelRoundFinishedEvent(), cancellationToken);
        await context.SendMessageAsync(new ModelOutcome(requests, finishReason, SubagentRunner.WithoutThinking(answer.ToString())), cancellationToken: cancellationToken);
    }

    private static TaskCompletionSource Completed()
    {
        var source = new TaskCompletionSource();
        source.SetResult();
        return source;
    }
}
