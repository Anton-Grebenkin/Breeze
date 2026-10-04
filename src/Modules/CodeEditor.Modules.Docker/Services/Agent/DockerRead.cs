namespace CodeEditor.Modules.Docker.Services.Agent;

/// <summary>A Docker read requested via the <c>docker</c> tool (<see cref="DockerCommands.Read"/>).</summary>
/// <param name="Name">logs: the container; inspect: a container, image, volume or network.</param>
/// <param name="Tail">logs, compose_logs: number of last lines.</param>
/// <param name="Since">logs: output since this time: "10m", "2h" or a date.</param>
public sealed record DockerRead(string Action, string? Name = null, int Tail = DockerCommands.DefaultTail, string? Since = null)
{
    /// <summary>compose_logs: only these services; empty means all.</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>compose_ps, compose_logs: compose file relative to the root with '/'.</summary>
    public string? ComposeFile { get; init; }
}
