using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Docker.Resources;

namespace CodeEditor.Modules.Docker.Services.Agent;

/// <summary>
/// docker arguments for tool actions. Model values never become docker options: container, image and service names may
/// not start with '-', and an option with its value is one argument (<c>--env=NAME=value</c>), so a value cannot become
/// another option. The container command goes after the image or container, where docker no longer parses it. compose
/// always gets an explicit file: otherwise docker searches parent folders and may stop someone else's project.
/// </summary>
public static class DockerCommands
{
    public const string Ps = "ps";
    public const string Images = "images";
    public const string Logs = "logs";
    public const string Inspect = "inspect";
    public const string ComposePs = "compose_ps";
    public const string ComposeLogs = "compose_logs";
    public const string Build = "build";
    public const string Run = "run";
    public const string Exec = "exec";
    public const string Start = "start";
    public const string Stop = "stop";
    public const string Restart = "restart";
    public const string Remove = "rm";
    public const string ComposeUp = "compose_up";
    public const string ComposeDown = "compose_down";

    public const int DefaultTail = 200;
    public const int MaxTail = 2000;

    // Shorter than the default tables: the model needs name, image, state and ports, not the full ID and command.
    private const string ContainersFormat = @"table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}";
    private const string ImagesFormat = @"table {{.Repository}}:{{.Tag}}\t{{.ID}}\t{{.CreatedSince}}\t{{.Size}}";

    public static IReadOnlyList<string> ReadActions { get; } = [Ps, Images, Logs, Inspect, ComposePs, ComposeLogs];

    public static IReadOnlyList<string> ChangeActions { get; } = [Build, Run, Exec, Start, Stop, Restart, Remove, ComposeUp, ComposeDown];

    /// <summary>Compose files docker looks for in a project folder, in its order.</summary>
    public static IReadOnlyList<string> ComposeFiles { get; } = ["compose.yaml", "compose.yml", "docker-compose.yaml", "docker-compose.yml"];

    public static bool IsCompose(string action) => action is ComposePs or ComposeLogs or ComposeUp or ComposeDown;

    /// <summary>Actions that can take minutes: build, image pull, a command in a container.</summary>
    public static bool IsLong(string action) => action is Build or Run or Exec or ComposeUp;

    /// <exception cref="AgentToolException">Unknown action or invalid argument.</exception>
    public static IReadOnlyList<string> Read(DockerRead request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Action switch
        {
            Ps => ["ps", "--all", "--format", ContainersFormat],
            Images => ["images", "--format", ImagesFormat],
            Logs => ["logs", Tail(request.Tail), .. Option("--since", request.Since), Name(Required(request.Name, Logs, "name"))],
            Inspect => ["inspect", Name(Required(request.Name, Inspect, "name"))],
            ComposePs => [.. Compose(request.ComposeFile, ComposePs), "ps", "--all"],
            ComposeLogs => [.. Compose(request.ComposeFile, ComposeLogs), "logs", "--no-color", Tail(request.Tail), .. request.Services.Select(Name)],
            _ => throw Unknown(request.Action, ReadActions),
        };
    }

    /// <exception cref="AgentToolException">Unknown action or invalid argument.</exception>
    public static IReadOnlyList<string> Change(DockerChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return change.Action switch
        {
            Build => ["build", .. Option("--tag", change.Image), .. Option("--file", change.Dockerfile), Folder(change.Context)],
            Run => [.. RunOptions(change), Name(Required(change.Image, Run, "image")), .. change.Command],
            Exec => ["exec", Container(change), .. Command(change)],
            Start or Stop or Restart or Remove => [change.Action, Container(change)],
            ComposeUp => [.. Compose(change.ComposeFile, ComposeUp), "up", "--detach", .. Flag(change.Build, "--build"), .. change.Services.Select(Name)],
            ComposeDown => [.. Compose(change.ComposeFile, ComposeDown), "down"],
            _ => throw Unknown(change.Action, ChangeActions),
        };
    }

    /// <summary>Command line for the approval card; arguments with spaces or quotes are quoted.</summary>
    public static string Display(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return "docker " + string.Join(' ', arguments.Select(Quote));
    }

    // A detached container stays so its logs can be read; a one-off run is removed when the command ends.
    private static IReadOnlyList<string> RunOptions(DockerChange change) =>
    [
        "run",
        change.Detach ? "--detach" : "--rm",
        .. Option("--name", change.Name),
        .. change.Ports.Select(port => "--publish=" + Required(port, Run, "ports")),
        .. change.Env.Select(variable => "--env=" + Variable(variable)),
        .. change.Volumes.Select(volume => "--volume=" + Required(volume, Run, "volumes")),
        .. Option("--network", change.Network),
    ];

    private static IReadOnlyList<string> Compose(string? file, string action) => ["compose", "--file=" + Required(file, action, "file")];

    private static IReadOnlyList<string> Option(string key, string? value) => string.IsNullOrWhiteSpace(value) ? [] : [key + "=" + value.Trim()];

    private static IReadOnlyList<string> Flag(bool on, string flag) => on ? [flag] : [];

    private static string Tail(int tail) => "--tail=" + Math.Clamp(tail, 1, MaxTail).ToString(CultureInfo.InvariantCulture);

    // The build folder is positional: docker would take "-x" for an option.
    private static string Folder(string? context) => context switch
    {
        null => ".",
        ['-', ..] => "./" + context,
        _ => context,
    };

    private static string Container(DockerChange change) => Name(Required(change.Name, change.Action, "name"));

    private static IReadOnlyList<string> Command(DockerChange change) =>
        change.Command.Count > 0 && !string.IsNullOrWhiteSpace(change.Command[0])
            ? change.Command
            : throw new AgentToolException(Format(Strings.ArgumentRequired, change.Action, "command"));

    private static string Variable(string variable) =>
        variable.IndexOf('=', StringComparison.Ordinal) is > 0 and var separator && !variable[..separator].Any(char.IsWhiteSpace)
            ? variable
            : throw new AgentToolException(Format(Strings.InvalidVariable, variable));

    private static string Required(string? value, string action, string argument) =>
        string.IsNullOrWhiteSpace(value) ? throw new AgentToolException(Format(Strings.ArgumentRequired, action, argument)) : value.Trim();

    private static string Name(string value) =>
        value.Length == 0 || value.StartsWith('-') || value.Any(char.IsWhiteSpace)
            ? throw new AgentToolException(Format(Strings.InvalidName, value))
            : value;

    private static AgentToolException Unknown(string action, IEnumerable<string> allowed) =>
        new(Format(Strings.UnknownAction, action, string.Join(", ", allowed)));

    private static string Quote(string argument) =>
        argument.Length > 0 && !argument.Any(character => char.IsWhiteSpace(character) || character is '"' or '\'')
            ? argument
            : "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
