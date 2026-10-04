using System.Globalization;
using CodeEditor.Modules.Docker.Services.Agent;

namespace CodeEditor.Modules.Docker.Services.Cli;

/// <summary>
/// docker arguments for the panel (ADR 0033) beyond the agent actions (<see cref="DockerCommands"/>): container and image
/// lists as line-delimited JSON, compose project, logs, inspect, removal. Names and IDs come from docker's own output,
/// but the same rules apply: no leading '-' and no whitespace; the compose file is a path inside the workspace.
/// </summary>
public static class DockerQueries
{
    /// <summary>Number of last log lines shown before streaming new ones.</summary>
    public const int LogTail = 500;

    // Fields are listed explicitly: "{{json .}}" makes docker compute every container's size (slow), and it joins labels
    // into one comma-separated string although label values may contain commas.
    private const string ContainersFormat =
        "{\"id\":{{json .ID}},\"name\":{{json .Names}},\"image\":{{json .Image}},\"state\":{{json .State}}," +
        "\"status\":{{json .Status}},\"ports\":{{json .Ports}}," +
        "\"project\":{{json (.Label \"com.docker.compose.project\")}},\"service\":{{json (.Label \"com.docker.compose.service\")}}}";

    private const string ImagesFormat =
        "{\"id\":{{json .ID}},\"repository\":{{json .Repository}},\"tag\":{{json .Tag}},\"size\":{{json .Size}},\"created\":{{json .CreatedAt}}}";

    /// <summary>All containers including stopped ones, one JSON line each, with full IDs.</summary>
    public static IReadOnlyList<string> Containers { get; } = ["ps", "--all", "--no-trunc", "--format", ContainersFormat];

    /// <summary>Images, one JSON line each.</summary>
    public static IReadOnlyList<string> Images { get; } = ["images", "--format", ImagesFormat];

    /// <summary>The compose project as one JSON document (<c>name</c>, <c>services</c>).</summary>
    public static IReadOnlyList<string> ComposeConfig(string file) => ["compose", File(file), "config", "--format", "json"];

    public static IReadOnlyList<string> ComposeRestart(string file, string service) => ["compose", File(file), "restart", Name(service)];

    /// <summary>The last <see cref="LogTail"/> log lines; <paramref name="follow"/> adds new ones as they appear.</summary>
    public static IReadOnlyList<string> Logs(string container, bool follow) =>
        ["logs", "--tail=" + LogTail.ToString(CultureInfo.InvariantCulture), .. Flag(follow, "--follow"), Name(container)];

    public static IReadOnlyList<string> InspectContainer(string id) => ["container", "inspect", Name(id)];

    public static IReadOnlyList<string> InspectImage(string reference) => ["image", "inspect", Name(reference)];

    /// <summary>Removes a container; a running one needs <paramref name="force"/>, and docker stops it first.</summary>
    public static IReadOnlyList<string> RemoveContainer(string id, bool force) =>
        ["rm", .. Flag(force, "--force"), Name(id)];

    /// <summary>
    /// Removing by name:tag only untags; removing the last tag deletes the image. docker refuses to remove an image in use.
    /// </summary>
    public static IReadOnlyList<string> RemoveImage(string reference) => ["image", "rm", Name(reference)];

    /// <summary>The compose file as one argument: a path inside the workspace with '/'.</summary>
    /// <exception cref="ArgumentException">The path is empty, rooted or leaves the workspace.</exception>
    public static string File(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        return Path.IsPathRooted(file) || file.Split('/', '\\').Contains("..")
            ? throw new ArgumentException($"Compose file '{file}' is outside the workspace.", nameof(file))
            : "--file=" + file;
    }

    private static IReadOnlyList<string> Flag(bool on, string flag) => on ? [flag] : [];

    /// <exception cref="ArgumentException">The name is empty, starts with '-' or contains whitespace.</exception>
    public static string Name(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0 || value.StartsWith('-') || value.Any(char.IsWhiteSpace)
            ? throw new ArgumentException($"'{value}' is not a safe docker name.", nameof(value))
            : value;
    }
}
