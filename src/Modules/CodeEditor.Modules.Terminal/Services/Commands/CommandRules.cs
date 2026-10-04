using System.Collections.Frozen;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// User "always allow" rules for agent commands (ADR 0012), as in Claude Code and Copilot: a word-wise prefix, so
/// "dotnet test" matches "dotnet test --no-build" but not "dotnet testx". A rule applies to a chained command only if
/// the whole line is transparent and arguments stay inside the workspace. No rule is offered for shells,
/// interpreters, downloads, deletion or dangerous subcommands: the user approves those one by one.
/// </summary>
public static class CommandRules
{
    /// <summary>Tools whose rule is command plus subcommand: "dotnet test", "npm install".</summary>
    private static readonly FrozenSet<string> TwoWordTools = FrozenSet.ToFrozenSet(
        ["git", "dotnet", "npm", "yarn", "pnpm", "docker", "cargo", "go", "kubectl", "gh"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> NeverRule = FrozenSet.ToFrozenSet(
    [
        "powershell", "pwsh", "cmd", "bash", "sh", "wsl", "node", "python", "python3", "py", "ruby", "perl", "php",
        "iex", "invoke-expression", "start-process", "saps", "start", "curl", "wget", "iwr", "irm", "invoke-webrequest",
        "invoke-restmethod", "rm", "del", "erase", "rd", "rmdir", "remove-item", "ri", "npx", "runas", "reg",
        "set-content", "add-content", "out-file", "set-executionpolicy", "format",
    ], StringComparer.Ordinal);

    private static readonly FrozenSet<string> NeverRuleSubcommands = FrozenSet.ToFrozenSet(
    [
        "git push", "git reset", "git clean", "git checkout", "git restore", "git rebase", "git rm", "git branch",
        "git tag", "git stash", "git config", "git remote", "git filter-branch", "git update-ref", "npm run",
        "npm publish", "npm exec", "yarn run", "pnpm run", "pnpm exec", "dotnet nuget", "docker rm", "docker rmi",
        "docker system", "docker run", "docker exec",
    ], StringComparer.Ordinal);

    /// <summary>The command's first words equal the rule's words, ignoring case.</summary>
    public static bool Matches(string rule, IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(words);
        var ruleWords = rule.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (ruleWords.Length == 0 || ruleWords.Length > words.Count)
        {
            return false;
        }

        for (var index = 0; index < ruleWords.Length; index++)
        {
            if (!string.Equals(ReadOnlyCommands.CommandName(ruleWords[index]), ReadOnlyCommands.CommandName(words[index]), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Runs without asking if every command in the chain is read-only or matches a rule.</summary>
    public static bool IsAllowed(CommandShape shape, IReadOnlyCollection<string> rules)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(rules);
        return shape.IsTransparent && shape.Segments.All(words =>
            ReadOnlyCommands.IsReadOnly(words) || (ReadOnlyCommands.HasSafeArguments(words) && rules.Any(rule => Matches(rule, words))));
    }

    /// <summary>Rule for the "Always allow" button, from the first command in the chain that needs approval.</summary>
    /// <returns><c>null</c> if no rule is offered.</returns>
    public static string? Suggest(CommandShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var words = shape.IsTransparent ? shape.Segments.FirstOrDefault(segment => !ReadOnlyCommands.IsReadOnly(segment)) : null;
        if (words is null || !ReadOnlyCommands.HasSafeArguments(words))
        {
            return null;
        }

        var name = ReadOnlyCommands.CommandName(words[0]);
        if (NeverRule.Contains(name) || name.StartsWith('$') || name.IndexOfAny(['/', '\\', '.']) >= 0)
        {
            return null;
        }

        if (!TwoWordTools.Contains(name))
        {
            return name;
        }

        if (words.Count < 2 || words[1].StartsWith('-'))
        {
            return null;
        }

        var rule = name + " " + words[1].ToLowerInvariant();
        return NeverRuleSubcommands.Contains(rule) ? null : rule;
    }
}
