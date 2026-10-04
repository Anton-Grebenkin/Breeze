using System.Collections.Frozen;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Web;

/// <summary>
/// Page read approvals (ADR 0025): documentation and sites from <c>agent.webHosts</c> are read without asking, others
/// after a card with the URL. The URL itself can exfiltrate workspace data (e.g. on instructions injected into a page
/// read earlier), so the user sees an unfamiliar site first. "Always allow" adds the site to <c>agent.webHosts</c>.
/// </summary>
public sealed partial class WebApprovals(IOptionsMonitor<AgentOptions> options, ISettingsService settings, ILogger<WebApprovals> logger) : IAgentApprovalPolicy
{
    public const string HostsKey = AgentOptions.Section + ".webHosts";

    /// <summary>Documentation and sources the agent reads most often.</summary>
    public static FrozenSet<string> DefaultHosts { get; } = new[]
    {
        "learn.microsoft.com", "devblogs.microsoft.com", "github.com", "raw.githubusercontent.com", "gist.github.com",
        "www.nuget.org", "nuget.org", "stackoverflow.com", "developer.mozilla.org", "en.wikipedia.org", "ru.wikipedia.org",
        "docs.docker.com", "git-scm.com", "www.npmjs.com", "docs.python.org", "pypi.org", "platform.openai.com",
        "platform.claude.com", "docs.claude.com",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public bool CanDecide(string toolName) => toolName == WebAgentTools.FetchName;

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Host(arguments) is { } host
        && (DefaultHosts.Contains(host) || options.CurrentValue.WebHosts.Contains(host, StringComparer.OrdinalIgnoreCase));

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) => CanDecide(toolName) ? Host(arguments) : null;

    public void AllowAlways(string toolName, string rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        var hosts = options.CurrentValue.WebHosts;
        if (!CanDecide(toolName) || hosts.Contains(rule, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!settings.TrySetUserValue(HostsKey, (string[])[.. hosts, rule], out var error))
        {
            LogRuleNotSaved(logger, rule, error);
        }
    }

    /// <summary>The host of the URL in the arguments; <c>null</c> if there is no URL or it is not http(s).</summary>
    public static string? Host(IDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.TryGetValue("url", out var value) && Uri.TryCreate(value?.ToString(), UriKind.Absolute, out var url)
            && url.Scheme is "http" or "https"
            ? url.IdnHost
            : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Web host rule '{Rule}' was not saved: {Error}")]
    private static partial void LogRuleNotSaved(ILogger logger, string rule, string error);
}
