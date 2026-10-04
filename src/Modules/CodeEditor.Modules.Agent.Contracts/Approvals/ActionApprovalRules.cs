namespace CodeEditor.Modules.Agent.Contracts.Approvals;

/// <summary><see cref="ActionApprovalPolicy"/> rules for a changing tool whose action is in the <c>action</c> argument.</summary>
/// <param name="ToolName">The tool the policy decides for, e.g. <c>git_change</c>.</param>
/// <param name="SettingsKey">The "always allow" list in settings.json, e.g. <c>git.alwaysAllow</c>.</param>
/// <param name="Actions">The tool's actions; only these can be allowed permanently.</param>
/// <param name="AlwaysAsk">Actions that always go through a card: they reach outside or cannot be undone.</param>
public sealed record ActionApprovalRules(string ToolName, string SettingsKey, IReadOnlyList<string> Actions, IReadOnlyList<string> AlwaysAsk);
