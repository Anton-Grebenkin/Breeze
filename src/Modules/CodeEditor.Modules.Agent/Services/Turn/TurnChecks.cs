using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Checks before a turn ends (ADR 0010, 0012), when the model answered without tool calls: code edits must pass a
/// build, in Deep mode also tests, and then an independent reviewer looks at the changes. These are external signals,
/// not the model's self-assessment. An exhausted request limit does not extend the turn; a folder with nothing to
/// build (<see cref="IAgentVerifier"/>) does not require a build.
/// </summary>
public sealed class TurnChecks(VerificationGate gate, TurnBudget budget, IEnumerable<IAgentVerifier> verifiers, TodoList todos, DeepSupervisor deep)
{
    private AgentMode _mode;
    private bool _remindedOfPlan;
    private bool _remindedOfEmpty;
    private bool _auditDue;
    private bool _remindedOfTextCall;

    /// <param name="request">The user request without harness blocks; decides whether a requirements audit is due.</param>
    public void Begin(AgentMode mode, string? request = null)
    {
        _mode = mode;
        _remindedOfPlan = false;
        _remindedOfEmpty = false;
        _auditDue = TaskRequirements.AreSeveral(request);
        _remindedOfTextCall = false;
        gate.Begin();
    }

    /// <param name="report">The model's summary in this answer, for the Deep mode reviewer.</param>
    /// <param name="truncated">The answer hit the length limit and continuations did not help; an empty summary is expected.</param>
    /// <returns><c>null</c> if the turn ends without notes.</returns>
    public async Task<TurnCheck?> NextAsync(string report, bool truncated, CancellationToken cancellationToken)
    {
        if (TextCall(report) is { } textCall)
        {
            return textCall;
        }

        if (!truncated && EmptyAnswer(report) is { } empty)
        {
            return empty;
        }

        if (!AgentModes.CanChange(_mode))
        {
            return null;
        }

        // Cheap counter check first, then whether anything can verify (walks the folder).
        var requireTests = _mode == AgentMode.Deep && CanVerify(VerificationKind.Tests);
        List<TurnCheck> due = [];
        if (gate.FindGap(requireTests) is { } gap && CanVerify(gap.Kind))
        {
            if (budget.IsExhausted || !gate.TryRemind())
            {
                return new TurnCheck(null, CorrectionHints.Unverified(gap));
            }

            due.Add(new TurnCheck(CorrectionHints.Reminder(gap, gate.CodeFilesInTurn), CorrectionHints.Status(gap)));
        }

        if (budget.IsExhausted)
        {
            return null;
        }

        // Both checks run: each one updates its own once-per-turn flag.
        if (OpenPlanItems(report) is { } plan)
        {
            due.Add(plan);
        }

        if (RequirementsAudit(report) is { } audit)
        {
            due.Add(audit);
        }

        return due.Count switch
        {
            0 => await deep.ReviewAsync(report, cancellationToken),
            1 => due[0],
            _ => Combine(due),
        };
    }

    /// <summary>
    /// Several checks go as one note: every extra round is a full request with the whole history.
    /// </summary>
    private static TurnCheck Combine(List<TurnCheck> checks) => new(
        string.Join("\n\n", checks.Select(static (check, index) => $"{index + 1}. {check.Prompt}")),
        string.Join(" · ", checks.Select(static check => check.Status)));

    /// <summary>
    /// A tool call written as text instead of a function call (<see cref="TextToolCall"/>) is not a summary: once per
    /// turn, in any mode, ask the model to call the tool properly.
    /// </summary>
    private TurnCheck? TextCall(string report)
    {
        if (_remindedOfTextCall || budget.IsExhausted || !TextToolCall.IsIn(report))
        {
            return null;
        }

        _remindedOfTextCall = true;
        return new TurnCheck(Strings.TextToolCallPrompt, Strings.TextToolCallStatus);
    }

    /// <summary>
    /// The turn ended with text while the plan still has open items: models sometimes report mid-work. One reminder
    /// per turn; a question to the user is a legitimate stop.
    /// </summary>
    private TurnCheck? OpenPlanItems(string report)
    {
        if (_remindedOfPlan || !todos.HasOpenItems || AsksUser(report))
        {
            return null;
        }

        _remindedOfPlan = true;
        var open = string.Join('\n', todos.Render().Split('\n').Where(static line => !line.StartsWith("[x]", StringComparison.Ordinal)));
        return new TurnCheck(string.Format(CultureInfo.CurrentCulture, Strings.OpenPlanItemsPrompt, open), Strings.OpenPlanItemsStatus);
    }

    /// <summary>
    /// The turn ended with an empty answer, no text and no calls: the user got neither a summary nor an explanation
    /// (models tend to drop the task after a tool error). One reminder per turn, in any mode.
    /// </summary>
    private TurnCheck? EmptyAnswer(string report)
    {
        if (_remindedOfEmpty || budget.IsExhausted || !string.IsNullOrWhiteSpace(report))
        {
            return null;
        }

        _remindedOfEmpty = true;
        return new TurnCheck(Strings.EmptyAnswerPrompt, Strings.EmptyAnswerStatus);
    }

    /// <summary>
    /// A point-by-point audit against the request before the summary (ADR 0016): for each requirement, where it is done
    /// and how it is verified. Not a self-assessment ("did I cover everything?" gets "yes") but locating the code, so a
    /// missed item shows up. Only for requests with several requirements and turns with code edits; once per turn;
    /// a question to the user is a legitimate stop.
    /// </summary>
    private TurnCheck? RequirementsAudit(string report)
    {
        if (!_auditDue || !gate.HasCodeEditsInTurn || AsksUser(report) || TaskRequirements.CitesEvidence(report))
        {
            return null;
        }

        _auditDue = false;
        return new TurnCheck(Strings.RequirementsAuditPrompt, Strings.RequirementsAuditStatus);
    }

    private static bool AsksUser(string report) => report.AsSpan().TrimEnd().EndsWith('?');

    private bool CanVerify(VerificationKind kind) =>
        verifiers.Any(verifier => kind == VerificationKind.Build ? verifier.CanBuild : verifier.CanTest);
}
