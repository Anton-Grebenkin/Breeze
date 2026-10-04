namespace CodeEditor.Modules.Docker.Services.Agent;

/// <summary>A Docker change requested via <c>docker_change</c> (<see cref="DockerCommands.Change"/>).</summary>
public sealed record DockerChange(string Action)
{
    /// <summary>run: name of the new container; exec, start, stop, restart, rm: the container.</summary>
    public string? Name { get; init; }

    /// <summary>run: the image; build: name and tag of the built image.</summary>
    public string? Image { get; init; }

    /// <summary>run: command replacing the image command; exec: the command and its arguments.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>run: "host:container" ports.</summary>
    public IReadOnlyList<string> Ports { get; init; } = [];

    /// <summary>run: "NAME=value" environment variables.</summary>
    public IReadOnlyList<string> Env { get; init; } = [];

    /// <summary>run: "volume:/path" or "host-path:/path" volumes.</summary>
    public IReadOnlyList<string> Volumes { get; init; } = [];

    /// <summary>run: the container network.</summary>
    public string? Network { get; init; }

    /// <summary>run: start in the background; <c>false</c> waits, returns the output and removes the container.</summary>
    public bool Detach { get; init; } = true;

    /// <summary>build: build folder relative to the root with '/'; <c>null</c> means the root.</summary>
    public string? Context { get; init; }

    /// <summary>build: Dockerfile relative to the root with '/'; <c>null</c> means the build folder's one.</summary>
    public string? Dockerfile { get; init; }

    /// <summary>compose_up, compose_down: compose file relative to the root with '/'.</summary>
    public string? ComposeFile { get; init; }

    /// <summary>compose_up: only these services; empty means all.</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>compose_up: rebuild images before starting.</summary>
    public bool Build { get; init; }
}
