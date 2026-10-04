namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>Container health check (<c>HEALTHCHECK</c>) from its status: "Up 2 hours (healthy)".</summary>
public enum ContainerHealth
{
    /// <summary>No health check.</summary>
    None,
    Starting,
    Healthy,
    Unhealthy,
}
