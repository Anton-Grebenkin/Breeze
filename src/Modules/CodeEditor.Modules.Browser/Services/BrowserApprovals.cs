using System.Globalization;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Approval for the <c>browser</c> tool (ADR 0027, 0054): pages on this machine (<c>localhost</c>, <c>127.0.0.1</c>,
/// <c>*.localhost</c>), sites the browser knows (<see cref="BrowserKnownSites"/>: the user opened them or let the agent
/// open them once) and sites in <c>browser.allowedHosts</c> open without asking; a new site needs a card once, since a
/// URL can carry data away. A script runs without asking only on a page of this machine: on a site where the user is logged in
/// it could send the session elsewhere. In its own tab the agent reads and follows links freely, and presses buttons
/// and fills in fields on a page of this machine or a site from <c>browser.actHosts</c>; elsewhere such an action needs
/// a card that names the button, with "always allow for this site". The user's tabs it reads freely, but going elsewhere, closing, or clicking and typing on a page that is
/// not of this machine needs a card that names the element. A request to a site's API (<c>request</c>) is free when it
/// only reads a site the agent may open; one that changes something always asks. A file goes into a form without asking only on a page of
/// this machine.
/// </summary>
public sealed partial class BrowserApprovals(
    IOptionsMonitor<BrowserOptions> options, BrowserAgentTabs tabs, ISettingsService settings, ILogger<BrowserApprovals> logger, BrowserKnownSites? known = null)
    : IAgentApprovalPolicy
{
    public const string AllowedHostsKey = BrowserOptions.Section + ".allowedHosts";
    public const string ActHostsKey = BrowserOptions.Section + ".actHosts";

    // "Always allow" rules offered for acting on a site, by the text shown on the card.
    private readonly Dictionary<string, string> _actRules = new(StringComparer.Ordinal);

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
        var site = tab?.Page.Url is { Scheme: "http" or "https" } page ? page.IdnHost : null;
        var isLocal = site is not null && IsLocal(site);
        var mayAct = site is null || isLocal || (!isUsers && options.CurrentValue.ActHosts.Contains(site, StringComparer.OrdinalIgnoreCase));
        return action switch
        {
            BrowserAgentTools.Navigate => !isUsers && Host(arguments) is { } host && IsAllowed(host),
            BrowserAgentTools.OpenTab => Text(arguments, "url") is not { Length: > 0 } || (Host(arguments) is { } host && IsAllowed(host)),
            BrowserAgentTools.Evaluate => isLocal,

            // Reading the API of a site the agent may open is like reading its pages; a request that changes
            // something is asked about every time.
            BrowserAgentTools.Request => isLocal || (site is not null && BrowserPageActions.IsReading(Text(arguments, "method")) && MayRead(site)),
            // Following a link is reading on; a button or a field acts on the site.
            BrowserAgentTools.Click => mayAct || (!isUsers && tabs.IsLink(tab!, Text(arguments, "ref"))),
            BrowserAgentTools.Type or BrowserAgentTools.Press or BrowserAgentTools.Drag or BrowserAgentTools.Dialog => mayAct,
            BrowserAgentTools.Hover or BrowserAgentTools.Back or BrowserAgentTools.Forward or BrowserAgentTools.Reload => !isUsers || isLocal,

            // A file put into a form leaves the machine with it.
            BrowserAgentTools.Upload => isLocal,
            BrowserAgentTools.CloseTab => !isUsers,
            _ => true,
        };
    }

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

    // Looking at pages and moving between them changes nothing, so it works in Ask and Plan too; pressing, typing,
    // scripts, files and requests that change a site do not. A new action is not a looking one until it is listed.
    public bool ReadsInEveryMode(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Action(arguments) switch
        {
            BrowserAgentTools.Navigate or BrowserAgentTools.Back or BrowserAgentTools.Forward or BrowserAgentTools.Reload
                or BrowserAgentTools.Snapshot or BrowserAgentTools.Read or BrowserAgentTools.Find or BrowserAgentTools.Screenshot
                or BrowserAgentTools.Scroll or BrowserAgentTools.Hover or BrowserAgentTools.WaitFor
                or BrowserAgentTools.Console or BrowserAgentTools.Network or BrowserAgentTools.Response
                or BrowserAgentTools.Tabs or BrowserAgentTools.OpenTab or BrowserAgentTools.SelectTab or BrowserAgentTools.CloseTab => true,
            BrowserAgentTools.Request => BrowserPageActions.IsReading(Text(arguments, "method")),
            _ => false,
        };

    // A request that changes something on a site and a file sent to one cannot be taken back: "allow for this chat"
    // does not cover them.
    public bool AlwaysAsks(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName)
        && (Action(arguments) == BrowserAgentTools.Upload || (Action(arguments) == BrowserAgentTools.Request && !BrowserPageActions.IsReading(Text(arguments, "method"))))
        && !IsPreapproved(toolName, arguments);

    // "Always allow" is about acting on a site in the agent's own tab. Opening a site needs none: a site allowed once
    // is remembered (BrowserKnownSites). A card about a tab of the user's offers none.
    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments)
    {
        if (!CanDecide(toolName))
        {
            return null;
        }

        var action = Action(arguments);
        var tab = tabs.Peek(Text(arguments, "tab"), opens: action is BrowserAgentTools.Navigate or BrowserAgentTools.OpenTab);
        if (tab is { IsAgent: false })
        {
            return null;
        }

        if (action is not (BrowserAgentTools.Click or BrowserAgentTools.Type or BrowserAgentTools.Press or BrowserAgentTools.Drag or BrowserAgentTools.Dialog)
            || tab?.Page.Url is not { Scheme: "http" or "https" } page || IsLocal(page.IdnHost))
        {
            return null;
        }

        var rule = string.Format(CultureInfo.CurrentCulture, Strings.ActRule, page.IdnHost);
        lock (_actRules)
        {
            _actRules[rule] = page.IdnHost;
        }

        return rule;
    }

    public void AllowAlways(string toolName, string rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        string? host;
        lock (_actRules)
        {
            host = _actRules.GetValueOrDefault(rule);
        }

        var hosts = options.CurrentValue.ActHosts;
        if (!CanDecide(toolName) || host is null || hosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!settings.TrySetUserValue(ActHostsKey, (string[])[.. hosts, host], out var error))
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

    private bool MayRead(string host) => IsAllowed(host) || options.CurrentValue.ActHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    private bool IsAllowed(string host) =>
        IsLocal(host) || known?.IsKnown(host) == true || options.CurrentValue.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

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
