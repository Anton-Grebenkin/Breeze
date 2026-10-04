using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Read-only commands the agent runs without asking (ADR 0012), modeled on Copilot, Claude Code and Codex lists:
/// PowerShell listings and info, git reads, .NET info, file names via rg. File contents are never read this way: a
/// glob like <c>*.env</c> would leak secrets that <c>read_file</c> and <c>search_text</c> skip. Every command in the
/// chain must be listed and its arguments must stay inside the workspace: no absolute paths, <c>..</c>, <c>~</c>,
/// drives like <c>env:</c>, or secret files. A guard against accidents, not a security boundary: anything
/// unrecognized goes to approval.
/// </summary>
public static partial class ReadOnlyCommands
{
    /// <summary>Cmdlets (and lowercase aliases) that neither write nor read file contents.</summary>
    private static readonly FrozenSet<string> Cmdlets = FrozenSet.ToFrozenSet(
    [
        "get-childitem", "gci", "ls", "dir", "test-path", "resolve-path", "rvpa", "get-item", "gi", "get-location", "gl",
        "pwd", "split-path", "join-path", "measure-object", "measure", "select-object", "select", "where-object", "where",
        "?", "foreach-object", "foreach", "%", "sort-object", "sort", "group-object", "group", "format-list", "fl",
        "format-table", "ft", "format-wide", "fw", "out-string", "out-null", "write-output", "echo", "write", "write-host",
        "get-date", "get-filehash", "get-command", "gcm", "convertfrom-json", "convertto-json", "get-unique",
    ], StringComparer.Ordinal);

    private static readonly FrozenSet<string> GitReadSubcommands = FrozenSet.ToFrozenSet(
        ["status", "diff", "log", "show", "blame", "ls-files", "rev-parse", "describe", "shortlog", "grep"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> GitGlobalOptions = FrozenSet.ToFrozenSet(["--no-pager", "-P", "--no-optional-locks"], StringComparer.Ordinal);

    /// <summary>git flags that write files or launch programs.</summary>
    private static readonly string[] GitUnsafePrefixes = ["--output", "--ext-diff", "--textconv", "-O", "--open-files-in-pager", "--exec", "--upload-pack", "--config"];

    private static readonly FrozenSet<string> GitBranchListing = FrozenSet.ToFrozenSet(
        ["-a", "-r", "-v", "-vv", "--list", "--all", "--remotes", "--show-current", "--verbose"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> DotNetInfo = FrozenSet.ToFrozenSet(["--info", "--version", "--list-sdks", "--list-runtimes"], StringComparer.Ordinal);

    /// <summary>rg modes that print file names and counts, not lines.</summary>
    private static readonly FrozenSet<string> RgNamesOnly = FrozenSet.ToFrozenSet(
        ["--files", "-l", "--files-with-matches", "-c", "--count", "--count-matches"], StringComparer.Ordinal);

    /// <summary>The whole line is read-only: transparent, every command listed, arguments safe.</summary>
    public static bool IsReadOnly(CommandShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape.IsTransparent && shape.Segments.All(IsReadOnly);
    }

    /// <summary>A single command of the chain is read-only.</summary>
    public static bool IsReadOnly(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (words.Count == 0 || !HasSafeArguments(words))
        {
            return false;
        }

        var name = CommandName(words[0]);
        return name switch
        {
            _ when name.StartsWith('$') => true,
            "rg" => words.Any(RgNamesOnly.Contains) && !words.Any(word => word.StartsWith("--pre", StringComparison.Ordinal) || word == "--hostname-bin"),
            "git" => IsGitRead(words),
            "dotnet" => words.Count > 1 && words.Skip(1).All(DotNetInfo.Contains),
            _ => Cmdlets.Contains(name),
        };
    }

    /// <summary>Arguments stay inside the workspace and avoid secrets (<c>-Path:value</c> checks the value).</summary>
    public static bool HasSafeArguments(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return words.Skip(1).All(IsSafeArgument);
    }

    /// <summary>Command name for comparison: lowercase, without <c>.exe</c>.</summary>
    public static string CommandName(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var name = word.ToLowerInvariant();
        return name.EndsWith(".exe", StringComparison.Ordinal) ? name[..^4] : name;
    }

    private static bool IsSafeArgument(string word)
    {
        var value = word.StartsWith('-') && word.IndexOf(':') is var colon and > 0 ? word[(colon + 1)..] : word;
        if (value.Length == 0 || value.StartsWith('-'))
        {
            return true;
        }

        // In git's "HEAD:src/.env" the path follows the colon.
        var path = value[(value.LastIndexOf(':') + 1)..].TrimEnd('/', '\\');
        return !(OutsidePath().IsMatch(value)
            || value.Contains("$env:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("$home", StringComparison.OrdinalIgnoreCase)
            || value.Contains("${", StringComparison.Ordinal)
            || value.Split('/', '\\').Any(part => part == "..")
            || SensitivePaths.IsSecret(value.TrimEnd('/', '\\'))
            || (path.Length > 0 && SensitivePaths.IsSecret(path)));
    }

    private static bool IsGitRead(IReadOnlyList<string> words)
    {
        var index = 1;
        while (index < words.Count && GitGlobalOptions.Contains(words[index]))
        {
            index++;
        }

        if (index >= words.Count)
        {
            return false;
        }

        var arguments = words.Skip(index + 1).ToList();
        if (arguments.Any(argument => GitUnsafePrefixes.Any(prefix => argument.StartsWith(prefix, StringComparison.Ordinal))))
        {
            return false;
        }

        return words[index] switch
        {
            "branch" => arguments.All(GitBranchListing.Contains),
            "remote" => arguments.All(argument => argument is "-v" or "--verbose"),
            "stash" => arguments.Count > 0 && arguments[0] is "list" or "show",
            var subcommand => GitReadSubcommands.Contains(subcommand),
        };
    }

    /// <summary>
    /// Absolute or network path, home folder, Windows drive (<c>C:</c>), PowerShell drive (<c>env:</c>, <c>HKLM:</c>…)
    /// or URL. git's "HEAD:path" is not a drive.
    /// </summary>
    [GeneratedRegex(@"^([\\/]|~|[a-z]:|(env|hklm|hkcu|hkey_\w+|registry|cert|variable|function|alias|wsman|temp):|[a-z][a-z0-9+.-]*://)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OutsidePath();
}
