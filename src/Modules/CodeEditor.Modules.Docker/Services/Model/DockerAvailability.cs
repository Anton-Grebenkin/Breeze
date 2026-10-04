namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>Whether Docker is usable right now; decides what the panel shows.</summary>
public enum DockerAvailability
{
    /// <summary>State not read yet.</summary>
    Unknown,
    Available,

    /// <summary>docker is not on PATH.</summary>
    NotInstalled,

    /// <summary>docker exists but the engine does not respond: Docker Desktop is not running.</summary>
    EngineStopped,

    /// <summary>docker returned an error; the text is in <see cref="DockerSnapshot.Error"/>.</summary>
    Failed,
}
