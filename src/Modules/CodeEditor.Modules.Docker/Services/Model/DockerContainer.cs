using CodeEditor.Modules.Docker.Services.Cli;

namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>A container from <c>docker ps --all</c> (<see cref="DockerQueries.Containers"/>).</summary>
/// <param name="Id">Full ID: the panel manages the container by it, since the name can change.</param>
/// <param name="Status">Status as docker writes it: "Up 45 hours", "Exited (0) 2 weeks ago".</param>
public sealed record DockerContainer(string Id, string Name, string Image, ContainerState State, string Status)
{
    private const int ShortIdLength = 12;

    /// <summary>Published ports without duplicates: docker lists IPv4 and IPv6 separately.</summary>
    public IReadOnlyList<PublishedPort> Ports { get; init; } = [];

    /// <summary>compose project (label <c>com.docker.compose.project</c>); <c>null</c> when not from compose.</summary>
    public string? Project { get; init; }

    /// <summary>compose service (label <c>com.docker.compose.service</c>).</summary>
    public string? Service { get; init; }

    public ContainerStatus Details => ContainerStatus.Parse(Status);

    /// <summary>Running, paused or restarting: the container can be stopped.</summary>
    public bool IsRunning => State is ContainerState.Running or ContainerState.Paused or ContainerState.Restarting;

    /// <summary>12 characters, as in docker output.</summary>
    public string ShortId => Id.Length > ShortIdLength ? Id[..ShortIdLength] : Id;
}
