namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>
/// A workspace compose file and the project it describes (<c>docker compose config</c>): the project name, which docker
/// uses to label containers (<see cref="DockerContainer.Project"/>), and the services.
/// </summary>
/// <param name="File">Path relative to the root with '/', as passed to docker (<c>--file=</c>).</param>
/// <param name="Name">Project name; <c>null</c> when the file could not be read (<see cref="Error"/>).</param>
public sealed record ComposeProject(string File, string? Name, IReadOnlyList<string> Services)
{
    /// <summary>Why docker could not read the file: a YAML error, a missing variable.</summary>
    public string? Error { get; init; }

    public static ComposeProject Failed(string file, string error) => new(file, null, []) { Error = error };
}
