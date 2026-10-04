using System.Collections.Frozen;
using CodeEditor.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Contracts.Approvals;

/// <summary>
/// Approval for a changing tool whose action is in the <c>action</c> argument (<c>git_change</c>, <c>docker_change</c>):
/// every call goes through a card, and "Always allow" adds the action to the module's settings list. Actions in
/// <see cref="ActionApprovalRules.AlwaysAsk"/> are never allowed permanently, even if added to settings by hand, and
/// never skip the card after "Allow for this chat".
/// </summary>
/// <param name="allowed">The current "always allow" list from the module settings.</param>
public sealed partial class ActionApprovalPolicy(ActionApprovalRules rules, Func<IReadOnlyList<string>> allowed, ISettingsService settings, ILogger<ActionApprovalPolicy> logger)
    : IAgentApprovalPolicy
{
    private readonly FrozenSet<string> _actions = rules.Actions.ToFrozenSet(StringComparer.Ordinal);
    private readonly FrozenSet<string> _alwaysAsk = rules.AlwaysAsk.ToFrozenSet(StringComparer.Ordinal);

    public bool CanDecide(string toolName) => toolName == rules.ToolName;

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) =>
        Allowable(toolName, arguments) is { } action && allowed().Contains(action, StringComparer.Ordinal);

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) => Allowable(toolName, arguments);

    public bool AlwaysAsks(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && arguments.TryGetValue("action", out var value) && value?.ToString() is { } action && _alwaysAsk.Contains(action);

    public void AllowAlways(string toolName, string rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        var current = allowed();
        if (!CanDecide(toolName) || !IsAllowable(rule) || current.Contains(rule, StringComparer.Ordinal))
        {
            return;
        }

        if (!settings.TrySetUserValue(rules.SettingsKey, (string[])[.. current, rule], out var error))
        {
            LogRuleNotSaved(logger, rules.SettingsKey, rule, error);
        }
    }

    /// <summary>Registers the policy; the "always allow" list comes from the module options <typeparamref name="TOptions"/>.</summary>
    public static void Register<TOptions>(IServiceCollection services, ActionApprovalRules rules, Func<TOptions, IReadOnlyList<string>> allowed)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAgentApprovalPolicy>(provider => new ActionApprovalPolicy(
            rules,
            () => allowed(provider.GetRequiredService<IOptionsMonitor<TOptions>>().CurrentValue),
            provider.GetRequiredService<ISettingsService>(),
            provider.GetRequiredService<ILogger<ActionApprovalPolicy>>()));
    }

    private string? Allowable(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && arguments.TryGetValue("action", out var value) && value?.ToString() is { Length: > 0 } action && IsAllowable(action)
            ? action
            : null;

    private bool IsAllowable(string action) => _actions.Contains(action) && !_alwaysAsk.Contains(action);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rule '{Rule}' was not saved to {Key}: {Error}")]
    private static partial void LogRuleNotSaved(ILogger logger, string key, string rule, string error);
}
