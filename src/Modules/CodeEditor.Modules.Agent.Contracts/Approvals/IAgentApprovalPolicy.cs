namespace CodeEditor.Modules.Agent.Contracts.Approvals;

/// <summary>
/// Tool call approval decided by the owning module (ADR 0012), like the permission rules of Claude Code and Copilot. A
/// call that surely changes nothing (a read-only command) or matches a user rule runs without a card; for others the
/// module suggests an "always allow" rule.
/// </summary>
public interface IAgentApprovalPolicy
{
    bool CanDecide(string toolName);

    /// <summary>The call may run without asking.</summary>
    bool IsPreapproved(string toolName, IDictionary<string, object?> arguments);

    /// <summary>The call only reads (ignoring user rules), so the scout may be trusted with it.</summary>
    bool IsReadOnly(string toolName, IDictionary<string, object?> arguments);

    /// <summary>Rule for the "Always allow" button (e.g. <c>dotnet test</c>); <c>null</c> to offer none.</summary>
    string? SuggestRule(string toolName, IDictionary<string, object?> arguments);

    /// <summary>The user chose "Always allow": the rule is saved to their settings.</summary>
    void AllowAlways(string toolName, string rule);

    /// <summary>
    /// The call always goes through a card, even if the user allowed the tool for this chat: it reaches outside or
    /// cannot be undone (<c>git push</c>, removing a container, replacing a whole document).
    /// </summary>
    bool AlwaysAsks(string toolName, IDictionary<string, object?> arguments) => false;
}
