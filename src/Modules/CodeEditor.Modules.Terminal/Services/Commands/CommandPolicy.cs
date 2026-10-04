using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Hard bans on agent commands, a deterministic layer before user approval: what undo or the recycle bin can't fix
/// never runs (formatting a disk, shutdown, force push, wiping a drive root, running a downloaded script).
/// Everything else the user sees on the card and decides.
/// </summary>
public static partial class CommandPolicy
{
    private static readonly (Regex Pattern, string Reason)[] Forbidden =
    [
        (FormatDisk(), Strings.PolicyFormatDisk),
        (PowerOff(), Strings.PolicyPowerOff),
        (ForcePush(), Strings.PolicyForcePush),
        (WipeRoot(), Strings.PolicyWipeRoot),
        (RemoteScript(), Strings.PolicyRemoteScript),
        (Registry(), Strings.PolicyRegistry),
        (ExecutionPolicy(), Strings.PolicyExecutionPolicy),
    ];

    /// <exception cref="AgentToolException">The command is forbidden; the message gives the reason.</exception>
    public static void EnsureAllowed(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        foreach (var (pattern, reason) in Forbidden)
        {
            if (pattern.IsMatch(command))
            {
                throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.CommandForbidden, reason));
            }
        }
    }

    [GeneratedRegex(@"\bformat(\.com)?\s+[a-z]:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormatDisk();

    [GeneratedRegex(@"\b(shutdown(\.exe)?|restart-computer|stop-computer)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PowerOff();

    [GeneratedRegex(@"\bgit\s+push\b.*(\s--force\b|\s-f\b|\s--force-with-lease\b|\s\+\S)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForcePush();

    [GeneratedRegex(@"(\brm\s+-[a-z]*r[a-z]*\s+(/|~|\$HOME|[a-z]:[\\/]?)(\s|$))|(\b(remove-item|rd|rmdir|del|erase)\b.*\s([a-z]:[\\/]?|\$env:(userprofile|systemroot)|~)(\s|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WipeRoot();

    [GeneratedRegex(@"(\b(iex|invoke-expression)\b.*\b(iwr|irm|invoke-webrequest|invoke-restmethod|downloadstring)\b)|(\b(iwr|irm|invoke-webrequest|invoke-restmethod|curl|wget)\b.*\|\s*(iex|invoke-expression|sh|bash|powershell|pwsh)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RemoteScript();

    [GeneratedRegex(@"\b(reg(\.exe)?\s+(add|delete|import)|set-itemproperty\s+.*hk(lm|cu):|remove-item\s+.*hk(lm|cu):)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Registry();

    [GeneratedRegex(@"\bset-executionpolicy\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExecutionPolicy();
}
