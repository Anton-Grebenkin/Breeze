using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Browser.Services.Agent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Approval for the <c>browser</c> tool (ADR 0027, 0054): pages on this machine (<c>localhost</c>, <c>127.0.0.1</c>,
/// <c>*.localhost</c>) and sites in <c>browser.allowedHosts</c> open without asking; others need a card, since a URL can
/// carry data away. A script runs without asking only on a page of this machine: on a site where the user is logged in
/// it could send the session elsewhere. In its own tab the agent clicks and types freely: the user sees the browser and
/// can step in. The user's tabs it reads freely, but going elsewhere, closing, or clicking and typing on a page that is
/// not of this machine needs a card that names the element.
/// </summary>
public sealed partial class BrowserApprovals(IOptionsMonitor<BrowserOptions> options, BrowserAgentTabs tabs, ISettingsService settings, ILogger<BrowserApprovals> logger)
    : IAgentApprovalPolicy
{
    public const string AllowedHostsKey = BrowserOptions.Section + ".allowedHosts";

    public bool CanDecide(string toolName) => toolName == BrowserAgentTools.ToolName;

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments)
    {
        if (!CanDecide(toolName))
        {
            return false;
        }

        var action = Action(arguments);
        var tab = tabs.Peek(Text(arguments, "tab"), opens: action == BrowserAgentTools.Navigate);
        var isUsers = tab is { IsAgent: false };
        var isLocal = tab?.Page.Url is { } page && IsLocal(page.IdnHost);
        return action switch
        {
            BrowserAgentTools.Navigate => !isUsers && Host(arguments) is { } host && IsAllowed(host),
            BrowserAgentTools.OpenTab => Text(arguments, "url") is not { Length: > 0 } || (Host(arguments) is { } host && IsAllowed(host)),
            BrowserAgentTools.Evaluate => isLocal,
            BrowserAgentTools.Click or BrowserAgentTools.Type or BrowserAgentTools.Press
                or BrowserAgentTools.Back or BrowserAgentTools.Forward or BrowserAgentTools.Reload => !isUsers || isLocal,
            BrowserAgentTools.CloseTab => !isUsers,
            _ => true,
        };
    }

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

    // "Always allow" is about the site; a card about a tab of the user's offers none.
    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Action(arguments) is BrowserAgentTools.Navigate or BrowserAgentTools.OpenTab && Host(arguments) is { } host && !IsLocal(host)
        && tabs.Peek(Text(arguments, "tab"), opens: true) is not { IsAgent: false }
            ? host
            : null;

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

    private static string? Action(IDictionary<string, object?> arguments) => Text(arguments, "action");

    private static string? Text(IDictionary<string, object?> arguments, string name) =>
        arguments.TryGetValue(name, out var value) ? value?.ToString() : null;

    private static string? Host(IDictionary<string, object?> arguments) =>
        arguments.TryGetValue("url", out var value) && Uri.TryCreate(value?.ToString(), UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
            ? url.IdnHost
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Browser host rule '{Rule}' was not saved: {Error}")]
    private static partial void LogRuleNotSaved(ILogger logger, string rule, string error);
}
