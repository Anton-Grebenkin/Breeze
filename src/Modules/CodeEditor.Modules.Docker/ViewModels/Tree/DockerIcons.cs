using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>Codicons of the Docker panel, and the icon with tone for a container state.</summary>
internal static class DockerIcons
{
    /// <summary>Activity bar icon: a container is a box.</summary>
    public const string Panel = "package";

    public const string Running = "vm-running";
    public const string Stopped = "vm-outline";
    public const string Paused = "debug-pause";
    public const string Restarting = "sync";
    public const string Failed = "error";
    public const string Project = "symbol-structure";
    public const string Image = "layers";
    public const string ComposeFile = "file-code";
    public const string Hint = "info";

    public static (string Icon, DockerTone Tone) Container(DockerContainer container) => container.State switch
    {
        ContainerState.Running when container.Details.Health == ContainerHealth.Unhealthy => (Running, DockerTone.Warning),
        ContainerState.Running => (Running, DockerTone.Running),
        ContainerState.Paused => (Paused, DockerTone.Warning),
        ContainerState.Restarting => (Restarting, DockerTone.Warning),
        ContainerState.Dead => (Failed, DockerTone.Error),
        ContainerState.Exited when container.Details.IsFailure => (Stopped, DockerTone.Error),
        _ => (Stopped, DockerTone.Muted),
    };
}
