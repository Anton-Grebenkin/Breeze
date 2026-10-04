using CodeEditor.Modules.Docker.Services.Agent;

namespace CodeEditor.Modules.Docker.Services;

/// <summary>Docker module settings: the <c>docker</c> section of settings.json.</summary>
public sealed class DockerOptions
{
    public const string Section = "docker";

    /// <summary>
    /// Changes the agent makes without a card ("Always allow"): build, run, start, stop, restart, compose_up,
    /// compose_down. exec and rm never go here (<see cref="DockerApprovals"/>).
    /// </summary>
    public List<string> AlwaysAllow { get; set; } = [];
}
