using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Hard bans on agent commands, a deterministic layer before user approval: what undo or the recycle bin can't fix
/// never runs (formatting a disk, shutdown, force push, wiping a drive root, running a downloaded script).
/// Everything else the user sees on the card and decides; a lease push to one's own branch does so with a warning
/// (<see cref="IsLeasePushToOwnBranch"/>).
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

    private static readonly string[] MainBranches = ["main", "master"];

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

        if (LeasePush().IsMatch(command) && !IsLeasePushToOwnBranch(command))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.CommandForbidden, Strings.PolicyLeasePush));
        }
    }

    /// <summary>
    /// <c>git push --force-with-lease origin my-branch</c>: rewriting one's own named branch, the usual step after a
    /// rebase. It is not banned, but always asks, with a warning on the card. Without a branch name the command does
    /// not say what will be rewritten, and to main or master it stays banned.
    /// </summary>
    public static bool IsLeasePushToOwnBranch(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pushes = CommandTokenizer.Parse(command).Segments
            .Where(static segment => segment.Count > 0 && ReadOnlyCommands.CommandName(segment[0]) == "git" && segment.Contains("push"))
            .Where(static segment => segment.Any(static word => word.StartsWith("--force-with-lease", StringComparison.Ordinal)))
            .ToList();
        return pushes.Count > 0 && pushes.All(static segment => NamesOwnBranches(segment));
    }

    // After "push" the plain words are the remote, then the branches ("src:dst" pushes to dst).
    private static bool NamesOwnBranches(IReadOnlyList<string> segment)
    {
        var branches = segment.SkipWhile(static word => word != "push").Skip(1).Where(static word => !word.StartsWith('-')).Skip(1).ToList();
        return branches.Count > 0 && branches.All(static branch =>
            branch[(branch.LastIndexOf(':') + 1)..] is { Length: > 0 } target && target != "HEAD" && !MainBranches.Contains(target.Replace("refs/heads/", string.Empty, StringComparison.Ordinal), StringComparer.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"\bformat(\.com)?\s+[a-z]:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormatDisk();

    [GeneratedRegex(@"\b(shutdown(\.exe)?|restart-computer|stop-computer)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PowerOff();

    [GeneratedRegex(@"\bgit\s+push\b.*(\s--force(?![-\w])|\s-f\b|\s\+\S)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForcePush();

    [GeneratedRegex(@"\bgit\s+push\b.*\s--force-with-lease\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeasePush();

    [GeneratedRegex(@"(\brm\s+-[a-z]*r[a-z]*\s+(/|~|\$HOME|[a-z]:[\\/]?)(\s|$))|(\b(remove-item|rd|rmdir|del|erase)\b.*\s([a-z]:[\\/]?|\$env:(userprofile|systemroot)|~)(\s|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WipeRoot();

    [GeneratedRegex(@"(\b(iex|invoke-expression)\b.*\b(iwr|irm|invoke-webrequest|invoke-restmethod|downloadstring)\b)|(\b(iwr|irm|invoke-webrequest|invoke-restmethod|curl|wget)\b.*\|\s*(iex|invoke-expression|sh|bash|powershell|pwsh)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RemoteScript();

    [GeneratedRegex(@"\b(reg(\.exe)?\s+(add|delete|import)|set-itemproperty\s+.*hk(lm|cu):|remove-item\s+.*hk(lm|cu):)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Registry();

    [GeneratedRegex(@"\bset-executionpolicy\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExecutionPolicy();
}
