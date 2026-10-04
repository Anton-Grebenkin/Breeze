using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Git.Resources;

namespace CodeEditor.Modules.Git.Services.Agent;

/// <summary>
/// git arguments for tool actions: a read is one command, a change is one or two (commit is add + commit). Model values
/// never become git options: revisions and branch names may not start with '-', paths go after "--".
/// </summary>
public static class GitCommands
{
    public const string Status = "status";
    public const string Diff = "diff";
    public const string Log = "log";
    public const string Show = "show";
    public const string Blame = "blame";
    public const string Commit = "commit";
    public const string CreateBranch = "create_branch";
    public const string Switch = "switch";
    public const string Stash = "stash";
    public const string StashPop = "stash_pop";
    public const string Restore = "restore";
    public const string Push = "push";

    public const int DefaultLogCount = 20;
    public const int MaxLogCount = 100;

    // Short hash, date, author, refs; the subject on the next line, indented.
    private const string LogFormat = "--format=%h %ad %an%d%n    %s";

    public static IReadOnlyList<string> ReadActions { get; } = [Status, Diff, Log, Show, Blame];

    public static IReadOnlyList<string> ChangeActions { get; } = [Commit, CreateBranch, Switch, Stash, StashPop, Restore, Push];

    /// <summary>Pushes a branch that has no upstream, creating it in <c>origin</c>.</summary>
    public static IReadOnlyList<string> PushWithUpstream { get; } = ["push", "--set-upstream", "origin", "HEAD"];

    /// <summary>Upstream of the current branch; exit code 0 means it exists.</summary>
    public static IReadOnlyList<string> Upstream { get; } = ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"];

    /// <exception cref="AgentToolException">Unknown action or invalid argument.</exception>
    public static IReadOnlyList<string> Read(GitRead request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Action switch
        {
            Status => ["status", "--short", "--branch"],
            Diff => ["diff", "--stat", "--patch", .. Flag(request.Staged, "--staged"), .. Revision(request.Revision), .. Paths(request.Path)],
            Log => ["log", "-n", Count(request.MaxCount), "--date=short", LogFormat, .. Revision(request.Revision), .. Paths(request.Path)],
            Show => ["show", "--stat", "--patch", "--date=short", .. Revision(request.Revision ?? "HEAD"), .. Paths(request.Path)],
            Blame => ["blame", "--date=short", .. Lines(request), "--", Required(request.Path, Blame, "path")],
            _ => throw Unknown(request.Action, ReadActions),
        };
    }

    /// <exception cref="AgentToolException">Unknown action or invalid argument.</exception>
    public static IReadOnlyList<IReadOnlyList<string>> Change(GitChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return change.Action switch
        {
            Commit => [Stage(change.Files), ["commit", "-m", Required(change.Message, Commit, "message")]],
            CreateBranch => [["switch", "-c", Name(Required(change.Branch, CreateBranch, "branch"), "branch")]],
            Switch => [["switch", Name(Required(change.Branch, Switch, "branch"), "branch")]],
            Stash => [string.IsNullOrWhiteSpace(change.Message) ? ["stash", "push"] : ["stash", "push", "-m", change.Message.Trim()]],
            StashPop => [["stash", "pop"]],
            Restore => [["restore", "--source=HEAD", "--staged", "--worktree", "--", Required(change.Path, Restore, "path")]],
            Push => [["push"]],
            _ => throw Unknown(change.Action, ChangeActions),
        };
    }

    /// <summary>Command line for the approval card; arguments with spaces or quotes are quoted.</summary>
    public static string Display(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return "git " + string.Join(' ', arguments.Select(Quote));
    }

    // New files are committed only when named: "all" means tracked files, so stray files are not picked up.
    private static IReadOnlyList<string> Stage(IReadOnlyList<string>? files) =>
        files is { Count: > 0 } ? ["add", "--", .. files] : ["add", "--update"];

    private static IReadOnlyList<string> Flag(bool on, string flag) => on ? [flag] : [];

    private static IReadOnlyList<string> Paths(string? path) => path is null ? [] : ["--", path];

    private static IReadOnlyList<string> Revision(string? revision) => revision is null ? [] : [Name(revision, "revision")];

    private static IReadOnlyList<string> Lines(GitRead request) => request.StartLine is { } start
        ? ["-L", request.EndLine is { } end ? Invariant($"{start},{end}") : Invariant($"{start},")]
        : [];

    private static string Count(int maxCount) => Math.Clamp(maxCount, 1, MaxLogCount).ToString(CultureInfo.InvariantCulture);

    private static string Required(string? value, string action, string argument) =>
        string.IsNullOrWhiteSpace(value) ? throw new AgentToolException(Format(Strings.ArgumentRequired, action, argument)) : value.Trim();

    private static string Name(string value, string kind) =>
        value.Length == 0 || value.StartsWith('-') || value.Any(char.IsWhiteSpace)
            ? throw new AgentToolException(Format(Strings.InvalidName, value, kind))
            : value;

    private static AgentToolException Unknown(string action, IEnumerable<string> allowed) =>
        new(Format(Strings.UnknownAction, action, string.Join(", ", allowed)));

    private static string Quote(string argument) =>
        argument.Length > 0 && !argument.Any(character => char.IsWhiteSpace(character) || character is '"' or '\'')
            ? argument
            : "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
