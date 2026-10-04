using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Rules;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Approval cards for agent requests, with a change preview from the module that owns the tool.
/// An edit that cannot apply (fragment not found) gets no card: the call runs at once and the model gets a clear error
/// to retry. Module policies (ADR 0012) decide which calls skip the card (read-only commands, user rules) and offer an
/// "Always allow" rule. agent.md (<see cref="ProjectRuleGuard"/>) can require a card with a reason and hold back an
/// edit until the model has seen the rules for its files.
/// </summary>
public sealed class ApprovalCards(IEnumerable<IAgentChangePreviewer> previewers, IEnumerable<IAgentApprovalPolicy> policies, ProjectRuleGuard? rules = null)
{
    /// <summary>The owning module allows the call without a card, and agent.md does not require asking.</summary>
    public bool IsPreapproved(ToolApprovalRequestContent request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (name, arguments) = Describe(request);
        return Policy(name)?.IsPreapproved(name, arguments) == true && rules?.AskReason(arguments, []) is null;
    }

    /// <summary>New chat: rules for files are sent to the model again.</summary>
    public void ResetChat() => rules?.Reset();

    /// <summary>The owning module requires a card for every such call, even after "Allow for this chat".</summary>
    public bool AlwaysAsks(ToolApprovalRequestContent request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (name, arguments) = Describe(request);
        return Policy(name)?.AlwaysAsks(name, arguments) == true;
    }

    /// <returns>The card, or <c>null</c> if the edit is invalid and should run without asking.</returns>
    public async Task<ApprovalCardViewModel?> CreateAsync(ToolApprovalRequestContent request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (name, arguments) = Describe(request);
        var previewer = previewers.FirstOrDefault(candidate => candidate.CanPreview(name));
        if (previewer is null)
        {
            var call = request.ToolCall as FunctionCallContent ?? new FunctionCallContent(string.Empty, name);
            var card = WithRule(new ApprovalCardViewModel(request, Format(Strings.ApprovalAllowTool, ToolCallText.Describe(call)), []), name, arguments);
            return WithProjectRules(card, arguments, []);
        }

        try
        {
            var changes = await previewer.PreviewAsync(name, arguments, cancellationToken);
            var files = changes.Select(change => new FileDiffViewModel(change)).ToList();
            var card = WithRule(new ApprovalCardViewModel(request, changes.FirstOrDefault()?.Title ?? Title(name, files.Count), files), name, arguments);
            return WithProjectRules(card, arguments, changes);
        }
        catch (AgentToolException)
        {
            return null;
        }
    }

    private static (string Name, IDictionary<string, object?> Arguments) Describe(ToolApprovalRequestContent request)
    {
        var call = request.ToolCall as FunctionCallContent;
        return (call?.Name ?? "tool", call?.Arguments ?? new Dictionary<string, object?>());
    }

    private IAgentApprovalPolicy? Policy(string toolName) => policies.FirstOrDefault(policy => policy.CanDecide(toolName));

    // "Always allow" button when the owning module suggests a rule.
    private ApprovalCardViewModel WithRule(ApprovalCardViewModel card, string name, IDictionary<string, object?> arguments)
    {
        if (Policy(name) is { } policy && policy.SuggestRule(name, arguments) is { } rule)
        {
            card.OfferRule(rule, () => policy.AllowAlways(name, rule));
        }

        return card;
    }

    private ApprovalCardViewModel WithProjectRules(ApprovalCardViewModel card, IDictionary<string, object?> arguments, IReadOnlyList<FileChangePreview> changes)
    {
        if (rules is null)
        {
            return card;
        }

        if (rules.RulesBeforeEdit(changes) is { } note)
        {
            card.HoldForRules(note);
        }
        else if (rules.AskReason(arguments, changes) is { } reason)
        {
            card.RequireDecision(reason);
        }

        return card;
    }

    private static string Title(string tool, int files) => tool switch
    {
        "apply_edits" => files == 1 ? Strings.ApprovalEdit : Format(Strings.ApprovalEdits, files),
        "create_file" => Strings.ApprovalCreateFile,
        "delete_file" => Strings.ApprovalDelete,
        "move_file" => Strings.ApprovalMove,
        "run_command" => Strings.ApprovalRunCommand,
        _ => Format(Strings.ApprovalRunTool, tool),
    };

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);
}
