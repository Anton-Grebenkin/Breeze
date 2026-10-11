using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Read-only commands the agent runs without asking (ADR 0012), modeled on the auto-approved lists of Copilot, Claude
/// Code and Codex: listings and info, navigation, reading named files, git, dotnet and docker reads, probes of this
/// machine (<see cref="ReadOnlyTools"/>). Every command in the chain must be listed and its arguments must stay inside
/// the workspace: no absolute paths, <c>..</c>, <c>~</c>, drives like <c>env:</c>, or secret files. Contents are read
/// only from files named outright: a wildcard or a file list piped into a reader could take in <c>.env</c>, which
/// <c>read_file</c> refuses. A guard against accidents, not a security boundary: anything unrecognized goes to approval.
/// </summary>
public static partial class ReadOnlyCommands
{
    /// <summary>Cmdlets (and lowercase aliases) that neither change anything nor read file contents.</summary>
    private static readonly FrozenSet<string> Cmdlets = FrozenSet.ToFrozenSet(
    [
        "get-childitem", "gci", "ls", "dir", "test-path", "resolve-path", "rvpa", "get-item", "gi", "get-location", "gl",
        "pwd", "split-path", "join-path", "measure-object", "measure", "select-object", "select", "where-object", "where",
        "?", "foreach-object", "foreach", "%", "sort-object", "sort", "group-object", "group", "format-list", "fl",
        "format-table", "ft", "format-wide", "fw", "out-string", "out-null", "out-host", "write-output", "echo", "write",
        "write-host", "get-date", "get-filehash", "get-command", "gcm", "convertfrom-json", "convertto-json", "get-unique",
        "compare-object", "convertfrom-csv", "convertto-csv", "test-json", "get-itemproperty", "gp", "start-sleep", "sleep",
        "set-location", "sl", "cd", "chdir", "push-location", "pushd", "pop-location", "popd", "get-process", "gps", "ps",
        "get-service", "gsv", "get-nettcpconnection", "get-help", "whoami", "hostname", "netstat", "tasklist",
    ], StringComparer.Ordinal);

    /// <summary>Commands that print file contents: only files named outright (<see cref="ReadsNamedFiles"/>).</summary>
    private static readonly FrozenSet<string> ContentReaders = FrozenSet.ToFrozenSet(
        ["get-content", "gc", "cat", "type", "select-string", "sls"], StringComparer.Ordinal);

    /// <summary>Commands that list files: piped into a reader, they would read every file they list.</summary>
    private static readonly FrozenSet<string> FileListers = FrozenSet.ToFrozenSet(
        ["get-childitem", "gci", "ls", "dir", "get-item", "gi"], StringComparer.Ordinal);

    /// <summary>rg modes that print file names and counts, not lines.</summary>
    private static readonly FrozenSet<string> RgNamesOnly = FrozenSet.ToFrozenSet(
        ["--files", "-l", "--files-with-matches", "-c", "--count", "--count-matches"], StringComparer.Ordinal);

    /// <summary>Flags of a content reader whose value is a path.</summary>
    private static readonly FrozenSet<string> PathFlags = FrozenSet.ToFrozenSet(["-path", "-literalpath", "-pspath", "-lp"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Switches of a content reader that change only how it reads or prints.</summary>
    private static readonly FrozenSet<string> ReaderSwitches = FrozenSet.ToFrozenSet(
        ["-simplematch", "-casesensitive", "-allmatches", "-notmatch", "-list", "-quiet", "-raw", "-noemphasis"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Flags of a content reader whose value is not a path (a regex, a count, an encoding).</summary>
    private static readonly FrozenSet<string> ReaderValueFlags = FrozenSet.ToFrozenSet(
        ["-pattern", "-tail", "-totalcount", "-head", "-first", "-last", "-encoding", "-context", "-delimiter", "-readcount", "-erroraction"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The whole line is read-only: no redirection, invocation or escapes, every command listed, arguments safe,
    /// and no listed files piped into a reader. Assignments are allowed when what they assign is read-only.
    /// </summary>
    public static bool IsReadOnly(CommandShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape is { HasRedirection: false, HasInvocation: false, HasEscapes: false }
            && shape.Segments.All(IsReadOnly)
            && !ReadsListedFiles(shape);
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
            _ when name.StartsWith('$') => IsVariableUse(words),
            "rg" => words.Any(RgNamesOnly.Contains) && !words.Any(word => word.StartsWith("--pre", StringComparison.Ordinal) || word == "--hostname-bin"),
            "git" => ReadOnlyTools.IsGitRead(words),
            "dotnet" => ReadOnlyTools.IsDotNetRead(words),
            "docker" => ReadOnlyTools.IsDockerRead(words),
            "test-netconnection" or "tnc" => ReadOnlyTools.IsLocalProbe(words),
            "invoke-webrequest" or "iwr" or "invoke-restmethod" or "irm" or "curl" => ReadOnlyTools.IsLocalHttpGet(words),
            _ when ContentReaders.Contains(name) => ReadsNamedFiles(name, words),
            _ => Cmdlets.Contains(name) || ReadOnlyTools.IsVersionQuery(words),
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

    // "$files" or "$f -eq 1" use a value; "$files = Get-ChildItem src" assigns a read-only command's output and
    // "$port = 5432" a literal. Another operator ("+=") or a changing command on the right goes to approval.
    private static bool IsVariableUse(IReadOnlyList<string> words)
    {
        if (words.Count < 2 || !words[1].EndsWith('='))
        {
            return true;
        }

        var value = words.Skip(2).ToList();
        return words[1] == "=" && Variable().IsMatch(words[0]) && value.Count > 0 && (IsLiteral(value) || IsReadOnly(value));
    }

    // A number, a value with spaces (a quoted text), or another variable.
    private static bool IsLiteral(List<string> value) =>
        value is [var single] && (Number().IsMatch(single) || single.Contains(' ', StringComparison.Ordinal) || Variable().IsMatch(single));

    // The paths are named outright: a wildcard or a variable could stand for a secret file. For Select-String the first
    // plain word is the pattern unless -Pattern names it. An unknown flag (an abbreviation, -Include) goes to approval.
    private static bool ReadsNamedFiles(string name, IReadOnlyList<string> words)
    {
        var patternPending = name is "select-string" or "sls" && !words.Any(static word => word.StartsWith("-pattern", StringComparison.OrdinalIgnoreCase));
        var paths = new List<string>();
        for (var index = 1; index < words.Count; index++)
        {
            var word = words[index];
            if (word.StartsWith('-'))
            {
                var colon = word.IndexOf(':');
                var flag = colon > 0 ? word[..colon] : word;
                if (PathFlags.Contains(flag))
                {
                    paths.Add(colon > 0 ? word[(colon + 1)..] : index + 1 < words.Count ? words[++index] : string.Empty);
                }
                else if (ReaderValueFlags.Contains(flag))
                {
                    index += colon > 0 ? 0 : 1;
                }
                else if (!ReaderSwitches.Contains(flag))
                {
                    return false;
                }

                continue;
            }

            if (patternPending)
            {
                patternPending = false;
                continue;
            }

            paths.Add(word);
        }

        return paths.All(static path => path.IndexOfAny(['*', '?', '[', '$']) < 0);
    }

    // A file list read by a reader, also through a variable: "$files = gci -Recurse; $files | sls key".
    private static bool ReadsListedFiles(CommandShape shape) =>
        shape.Segments.Any(segment => FileListers.Contains(CommandOf(segment)))
        && shape.Segments.Any(segment => ContentReaders.Contains(CommandOf(segment)));

    // The command of a segment, or of the right side of an assignment.
    private static string CommandOf(IReadOnlyList<string> segment) =>
        CommandName(segment is [_, "=", var command, ..] ? command : segment[0]);

    private static bool IsSafeArgument(string word)
    {
        var value = word.StartsWith('-') && word.IndexOf(':') is var colon and > 0 ? word[(colon + 1)..] : word;
        if (value.Length == 0 || value.StartsWith('-'))
        {
            return true;
        }

        // In git's "HEAD:src/.env" the path follows the colon; a URL is checked by the tool's own rule.
        var path = value[(value.LastIndexOf(':') + 1)..].TrimEnd('/', '\\');
        return !((OutsidePath().IsMatch(value) && !LocalUrl().IsMatch(value))
            || value.Contains("$env:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("$home", StringComparison.OrdinalIgnoreCase)
            || value.Contains("${", StringComparison.Ordinal)
            || value.Split('/', '\\').Any(part => part == "..")
            || SensitivePaths.IsSecret(value.TrimEnd('/', '\\'))
            || (path.Length > 0 && SensitivePaths.IsSecret(path)));
    }

    /// <summary>
    /// Absolute or network path, home folder, Windows drive (<c>C:</c>), PowerShell drive (<c>env:</c>, <c>HKLM:</c>…)
    /// or URL. git's "HEAD:path" is not a drive.
    /// </summary>
    [GeneratedRegex(@"^([\\/]|~|[a-z]:|(env|hklm|hkcu|hkey_\w+|registry|cert|variable|function|alias|wsman|temp):|[a-z][a-z0-9+.-]*://)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OutsidePath();

    /// <summary>A URL on this machine, which the HTTP rule may let through.</summary>
    [GeneratedRegex(@"^https?://(localhost|127\.0\.0\.1|\[::1\])(:\d+)?(/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalUrl();

    [GeneratedRegex(@"^\$[A-Za-z_]\w*$", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Number();
}
