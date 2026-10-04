using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Approval for the <c>browser</c> tool (ADR 0027): pages on this machine (<c>localhost</c>, <c>127.0.0.1</c>,
/// <c>*.localhost</c>) and sites in <c>browser.allowedHosts</c> open without asking; others need a card, since a URL can
/// carry data away. Actions on an open page need no approval: the user sees the browser and can step in.
/// </summary>
public sealed partial class BrowserApprovals(IOptionsMonitor<BrowserOptions> options, ISettingsService settings, ILogger<BrowserApprovals> logger) : IAgentApprovalPolicy
{
    public const string AllowedHostsKey = BrowserOptions.Section + ".allowedHosts";

    public bool CanDecide(string toolName) => toolName == BrowserAgentTools.ToolName;

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && (Action(arguments) != BrowserAgentTools.Navigate || Host(arguments) is { } host && IsAllowed(host));

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Action(arguments) == BrowserAgentTools.Navigate && Host(arguments) is { } host && !IsLocal(host) ? host : null;

    public void AllowAlways(string toolName, string rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        var hosts = options.CurrentValue.AllowedHosts;
        if (!CanDecide(toolName) || hosts.Contains(rule, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!settings.TrySetUserValue(AllowedHostsKey, (string[])[.. hosts, rule], out var error))
        {
            LogRuleNotSaved(logger, rule, error);
        }
    }

    /// <summary>A page on this machine: the agent is checking the app the user develops.</summary>
    public static bool IsLocal(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host is "localhost" or "127.0.0.1" or "[::1]" or "::1" || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAllowed(string host) => IsLocal(host) || options.CurrentValue.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    private static string? Action(IDictionary<string, object?> arguments) =>
        arguments.TryGetValue("action", out var value) ? value?.ToString() : null;

    private static string? Host(IDictionary<string, object?> arguments) =>
        arguments.TryGetValue("url", out var value) && Uri.TryCreate(value?.ToString(), UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
            ? url.IdnHost
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Browser host rule '{Rule}' was not saved: {Error}")]
    private static partial void LogRuleNotSaved(ILogger logger, string rule, string error);
}
