using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Workflow;

/// <summary>
/// The approvals node of the turn graph (ADR 0013): decides on model requests. Decided without asking: edits in a
/// read-only mode (refused with a reason), the first edit in Deep mode (goes to the advisor first), commands allowed by
/// a module policy, and tools allowed for the chat or by the accept-edits mode. The rest become cards sent to the
/// <c>approve</c> port; the user decides, and the responses go back to the model in one message.
/// </summary>
/// <param name="acceptEdits">Edits, file creation and moves approve themselves (<see cref="AgentApprovals.Auto"/>).</param>
public sealed class ApprovalExecutor(
    ApprovalCards cards,
    DeepSupervisor deep,
    AgentActivityLog log,
    ISet<string> approvedTools,
    bool acceptEdits,
    AgentMode mode,
    CancellationToken turn) : Executor(NodeId)
{
    public const string NodeId = "approvals";

    /// <summary>Tools that accept-edits mode approves itself: <c>Ctrl+Z</c> or a revert in the changes panel undoes them.</summary>
    private static readonly FrozenSet<string> EditTools = FrozenSet.ToFrozenSet(["apply_edits", "apply_patch", "create_file", "move_file"], StringComparer.Ordinal);

    private readonly List<(ToolApprovalRequestContent Request, AIContent? Response, ApprovalCardViewModel? Card)> _pending = [];

    /// <summary>Decisions in the turn, for the log.</summary>
    public int Count { get; private set; }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        protocolBuilder.SendsMessage<ApprovalPending>().SendsMessage<TurnInput>().ConfigureRoutes(routes => routes
            .AddHandler<ModelOutcome>(DecideAsync)
            .AddHandler<ApprovalDecided>((_, context, cancellationToken) => RespondAsync(context, cancellationToken)));

    private async ValueTask DecideAsync(ModelOutcome outcome, IWorkflowContext context, CancellationToken cancellationToken)
    {
        // The graph does not pass the turn's cancellation to nodes; the advisor and preview must stop with the turn.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, turn);
        cancellationToken = linked.Token;
        _pending.Clear();
        foreach (var request in outcome.Requests)
        {
            _pending.Add(await PrepareAsync(request, cancellationToken));
        }

        var waiting = _pending.Where(static pending => pending.Card is { IsPending: true }).Select(static pending => pending.Card!).ToList();
        if (waiting.Count > 0)
        {
            await context.SendMessageAsync(new ApprovalPending(waiting), cancellationToken: cancellationToken);
            return;
        }

        await RespondAsync(context, cancellationToken);
    }

    // A decision without the user, or a card; an automatic card is already decided.
    private async Task<(ToolApprovalRequestContent, AIContent?, ApprovalCardViewModel?)> PrepareAsync(ToolApprovalRequestContent request, CancellationToken cancellationToken)
    {
        if (!AgentModes.CanChange(mode))
        {
            return (request, request.CreateResponse(false, string.Format(CultureInfo.CurrentCulture, Strings.ChangesUnavailableInMode, AgentModes.Title(mode))), null);
        }

        // Deep mode: the turn's first edit goes to the advisor; the model gets the review and repeats the edit.
        if (request.ToolCall is FunctionCallContent call
            && await deep.BeforeEditAsync(call.Name, JsonSerializer.Serialize(call.Arguments, AIJsonUtilities.DefaultOptions), cancellationToken) is { } advice)
        {
            return (request, request.CreateResponse(false, advice), null);
        }

        // A read-only command or one allowed by a user rule needs no card: the feed already shows the tool line.
        if (cards.IsPreapproved(request))
        {
            log.ApprovedByPolicy((request.ToolCall as FunctionCallContent)?.Name ?? string.Empty);
            return (request, request.CreateResponse(true), null);
        }

        var card = await cards.CreateAsync(request, cancellationToken);
        if (card is null)
        {
            // The edit cannot apply: the model gets the tool error and fixes the call.
            return (request, request.CreateResponse(true), null);
        }

        // The model has not seen the rules for these files: it gets them and repeats the change.
        if (card.PendingRules is { } rules)
        {
            log.HeldForRules(card.ToolName);
            return (request, request.CreateResponse(false, rules), null);
        }

        // An edit accepted without asking shows as a tool line ("Changed A.cs +1 −1"). A call the module always confirms
        // (git push) or that agent.md requires still asks after "Allow for this chat".
        if (!card.HasReason && !cards.AlwaysAsks(request) && (approvedTools.Contains(card.ToolName) || (acceptEdits && EditTools.Contains(card.ToolName))))
        {
            card.ResolveAutomatically();
        }

        return (request, null, card);
    }

    private async ValueTask RespondAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        var responses = new List<AIContent>(_pending.Count);
        foreach (var (request, response, card) in _pending)
        {
            responses.Add(response ?? await DecidedAsync(request, card!, cancellationToken));
        }

        _pending.Clear();
        await context.SendMessageAsync(new TurnInput(new ChatMessage(ChatRole.User, responses)), cancellationToken: cancellationToken);
    }

    private async Task<AIContent> DecidedAsync(ToolApprovalRequestContent request, ApprovalCardViewModel card, CancellationToken cancellationToken)
    {
        var approved = await card.Decision.WaitAsync(cancellationToken);
        Count++;
        log.ApprovalDecided(card.ToolName, card.Files.Count, approved, card.IsAutomatic, card.ApprovedForChat);
        if (card.ApprovedForChat)
        {
            approvedTools.Add(card.ToolName);
        }

        if (card is { AllowedAlways: true, Rule: { } rule })
        {
            log.RuleAllowed(card.ToolName, rule);
        }

        return request.CreateResponse(approved, approved ? null : Strings.UserRejectedAction);
    }
}
