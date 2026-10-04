namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>Docker state at read time (<see cref="DockerStateReader"/>): what the panel shows.</summary>
/// <param name="Compose">Workspace compose files; empty without an open folder.</param>
public sealed record DockerSnapshot(
    DockerAvailability Availability,
    IReadOnlyList<DockerContainer> Containers,
    IReadOnlyList<DockerImage> Images,
    IReadOnlyList<ComposeProject> Compose)
{
    /// <summary>First line of the docker error for <see cref="DockerAvailability.Failed"/>.</summary>
    public string? Error { get; init; }

    public static DockerSnapshot Unavailable(DockerAvailability availability, string? error = null) =>
        new(availability, [], [], []) { Error = error };
}
