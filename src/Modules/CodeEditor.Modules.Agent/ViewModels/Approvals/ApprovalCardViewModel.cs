using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.ViewModels.Approvals;

/// <summary>
/// An approval card for an agent action: what will change (per-file diffs) and the user's decision: apply, reject,
/// allow the tool for this chat, or, when a module offers a rule, always allow it (ADR 0012).
/// </summary>
public sealed partial class ApprovalCardViewModel : ObservableObject
{
    private readonly TaskCompletionSource<bool> _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Action? _allowAlways;

    public ApprovalCardViewModel(ToolApprovalRequestContent request, string title, IReadOnlyList<FileDiffViewModel> files)
    {
        Request = request;
        Title = title;
        Files = files;
    }

    public ToolApprovalRequestContent Request { get; }

    public string ToolName => (Request.ToolCall as FunctionCallContent)?.Name ?? string.Empty;

    public string Title { get; }

    public IReadOnlyList<FileDiffViewModel> Files { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(StateText))]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand), nameof(RejectCommand), nameof(ApproveForChatCommand), nameof(AllowAlwaysCommand))]
    public partial ApprovalState State { get; private set; }

    /// <summary>A module's "always allow" rule (e.g. <c>dotnet test</c>); <c>null</c> hides the button.</summary>
    public string? Rule { get; private set; }

    public bool HasRule => Rule is not null;

    public string AllowAlwaysText => Rule is null ? string.Empty : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.AllowAlways, Rule);

    public bool AllowedAlways { get; private set; }

    /// <summary>Why agent.md requires the user's decision; <c>null</c> for a regular card.</summary>
    public string? Reason { get; private set; }

    public bool HasReason => Reason is not null;

    /// <summary>Rules the model must read before this change; the call goes back to it without a card.</summary>
    public string? PendingRules { get; private set; }

    /// <summary>agent.md requires a decision: the card asks even when the tool is allowed for the chat.</summary>
    public void RequireDecision(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason;
    }

    public void HoldForRules(string rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rules);
        PendingRules = rules;
    }

    /// <summary>Shows the "Always allow" button; <paramref name="allowAlways"/> saves the rule on click.</summary>
    public void OfferRule(string rule, Action allowAlways)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        Rule = rule;
        _allowAlways = allowAlways;
    }

    /// <summary>The user allowed this tool until the end of the chat.</summary>
    public bool ApprovedForChat { get; private set; }

    /// <summary>Resolved without a click: the tool is allowed for the chat or "Accept edits" is on.</summary>
    public bool IsAutomatic { get; private set; }

    public bool IsPending => State == ApprovalState.Pending;

    /// <summary>The card is about a command rather than an edit: "Run" button, "Allowed" result.</summary>
    public bool IsCommand => Files.Count > 0 && Files.All(file => file.IsCommand);

    public string ApproveText => IsCommand ? Strings.RunCommand : Strings.Apply;

    public string StateText => State switch
    {
        ApprovalState.Approved when IsAutomatic => IsCommand ? Strings.ApprovalAllowedAutomatically : Strings.ApprovalAppliedAutomatically,
        ApprovalState.Approved => IsCommand ? Strings.ApprovalAllowed : Strings.ApprovalApplied,
        ApprovalState.Rejected => Strings.ApprovalRejected,
        _ => string.Empty,
    };

    /// <summary>The decision; <c>true</c> means apply.</summary>
    public Task<bool> Decision => _decision.Task;

    /// <summary>Approves without asking: allowed for the chat earlier or "Accept edits" is on.</summary>
    public void ResolveAutomatically()
    {
        IsAutomatic = IsPending;
        Resolve(approved: true);
    }

    /// <summary>Decides without a click, e.g. when the chat is stopped.</summary>
    public void Resolve(bool approved)
    {
        if (!IsPending)
        {
            return;
        }

        State = approved ? ApprovalState.Approved : ApprovalState.Rejected;
        _decision.TrySetResult(approved);
    }

    [RelayCommand(CanExecute = nameof(IsPending))]
    private void Approve() => Resolve(approved: true);

    [RelayCommand(CanExecute = nameof(IsPending))]
    private void Reject() => Resolve(approved: false);

    [RelayCommand(CanExecute = nameof(IsPending))]
    private void ApproveForChat()
    {
        ApprovedForChat = true;
        Resolve(approved: true);
    }

    [RelayCommand(CanExecute = nameof(IsPending))]
    private void AllowAlways()
    {
        AllowedAlways = true;
        _allowAlways?.Invoke();
        Resolve(approved: true);
    }
}
