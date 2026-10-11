using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Read-only uses of tools with subcommands (<see cref="ReadOnlyCommands"/>): git, dotnet and docker reads, version
/// queries, and probes of this machine — a port check or an HTTP GET to <c>localhost</c>, like the browser tool's local
/// pages. A host other than this machine could carry data away (in a DNS name or a URL), so it goes to approval.
/// </summary>
public static partial class ReadOnlyTools
{
    private static readonly FrozenSet<string> GitReadSubcommands = FrozenSet.ToFrozenSet(
    [
        "status", "diff", "log", "show", "blame", "ls-files", "ls-tree", "rev-parse", "rev-list", "describe", "shortlog",
        "grep", "merge-base", "name-rev", "cat-file", "show-ref", "for-each-ref", "check-ignore", "count-objects",
    ], StringComparer.Ordinal);

    private static readonly FrozenSet<string> GitGlobalOptions = FrozenSet.ToFrozenSet(["--no-pager", "-P", "--no-optional-locks"], StringComparer.Ordinal);

    /// <summary>git flags that write files or launch programs.</summary>
    private static readonly string[] GitUnsafePrefixes = ["--output", "--ext-diff", "--textconv", "-O", "--open-files-in-pager", "--exec", "--upload-pack", "--config"];

    private static readonly FrozenSet<string> GitBranchListing = FrozenSet.ToFrozenSet(
        ["-a", "-r", "-v", "-vv", "--list", "--all", "--remotes", "--show-current", "--verbose"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> DotNetInfo = FrozenSet.ToFrozenSet(["--info", "--version", "--list-sdks", "--list-runtimes"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> DockerReads = FrozenSet.ToFrozenSet(["ps", "images", "logs", "inspect", "version", "info", "top", "port"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> DockerComposeReads = FrozenSet.ToFrozenSet(["ps", "logs", "images", "top", "version", "ls"], StringComparer.Ordinal);

    /// <summary>Tools whose <c>--version</c> only prints it.</summary>
    private static readonly FrozenSet<string> VersionTools = FrozenSet.ToFrozenSet(
        ["node", "npm", "pnpm", "yarn", "python", "py", "java", "go", "cargo", "rustc", "pwsh", "powershell", "kubectl", "gh", "az"], StringComparer.Ordinal);

    /// <summary>Flags of <c>Invoke-WebRequest</c>, <c>Invoke-RestMethod</c> and curl that don't send data or write files.</summary>
    private static readonly FrozenSet<string> HttpGetFlags = FrozenSet.ToFrozenSet(
    [
        "-uri", "-usebasicparsing", "-timeoutsec", "-method", "-headers", "-erroraction", "-maximumredirection", "-skiphttperrorcheck",
        "-s", "-S", "-i", "-I", "-f", "-L", "-v", "--silent", "--show-error", "--include", "--head", "--fail", "--location", "--max-time", "-m",
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>Flags whose value comes next, checked by its own rule.</summary>
    private static readonly FrozenSet<string> HttpValueFlags = FrozenSet.ToFrozenSet(
        ["-timeoutsec", "-method", "-headers", "-erroraction", "-maximumredirection", "--max-time", "-m"], StringComparer.OrdinalIgnoreCase);

    public static bool IsGitRead(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
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
            "worktree" => arguments is ["list", ..],
            "reflog" => arguments.Count == 0 || arguments[0] == "show",
            var subcommand => GitReadSubcommands.Contains(subcommand),
        };
    }

    /// <summary><c>dotnet --info</c> and the like, <c>dotnet list package</c>, <c>dotnet sln list</c>.</summary>
    public static bool IsDotNetRead(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return words.Count > 1 && (words.Skip(1).All(DotNetInfo.Contains) || words[1] == "list" || (words[1] == "sln" && words.Contains("list")));
    }

    /// <summary><c>docker ps</c>, <c>docker logs</c>, <c>docker compose ps</c>… — no changes and no secrets from config.</summary>
    public static bool IsDockerRead(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var arguments = words.Skip(1).SkipWhile(word => word.StartsWith('-')).ToList();
        return arguments switch
        {
            ["compose", .. var rest] => rest.SkipWhile(word => word.StartsWith('-')).FirstOrDefault() is { } command && DockerComposeReads.Contains(command),
            [var command, ..] => DockerReads.Contains(command),
            _ => false,
        };
    }

    /// <summary><c>node --version</c>, <c>npm -v</c>.</summary>
    public static bool IsVersionQuery(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return words.Count == 2 && VersionTools.Contains(ReadOnlyCommands.CommandName(words[0])) && words[1] is "--version" or "-v" or "-V" or "version";
    }

    /// <summary><c>Test-NetConnection localhost -Port 5432</c>: every value names this machine or is a number.</summary>
    public static bool IsLocalProbe(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return Expand(words).Skip(1).Where(word => !word.StartsWith('-')).All(word => IsLocalHost(word) || ProbeValue().IsMatch(word));
    }

    /// <summary>An HTTP GET to this machine that neither sends a body nor writes a file.</summary>
    public static bool IsLocalHttpGet(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var expanded = Expand(words);
        var urls = 0;
        for (var index = 1; index < expanded.Count; index++)
        {
            var word = expanded[index];
            if (word.StartsWith('-'))
            {
                if (!HttpGetFlags.Contains(word))
                {
                    return false;
                }

                if (HttpValueFlags.Contains(word))
                {
                    index++;
                    if (word.Equals("-method", StringComparison.OrdinalIgnoreCase) && !(index < expanded.Count && expanded[index].Equals("get", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }

                continue;
            }

            if (!Uri.TryCreate(word, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") || !IsLocalHost(url.Host))
            {
                return false;
            }

            urls++;
        }

        return urls > 0;
    }

    // "-Uri:http://x" is "-Uri" and its value: the value is checked like any other.
    private static List<string> Expand(IReadOnlyList<string> words) =>
    [
        .. words.SelectMany(static word => word.StartsWith('-') && word.IndexOf(':') is var colon and > 1 ? [word[..colon], word[(colon + 1)..]] : new[] { word }),
    ];

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host is "127.0.0.1" or "::1" or "[::1]";

    // A port, a list of ports, or a switch value such as SilentlyContinue.
    [GeneratedRegex(@"^(\d+(,\d+)*|SilentlyContinue|Quiet|Detailed)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProbeValue();
}
