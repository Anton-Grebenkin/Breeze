namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>Container state from <c>docker ps</c> (the <c>State</c> field).</summary>
public enum ContainerState
{
    Unknown,
    Created,
    Running,
    Paused,
    Restarting,
    Removing,
    Exited,
    Dead,
}
