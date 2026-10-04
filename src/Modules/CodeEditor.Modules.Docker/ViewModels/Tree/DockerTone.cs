namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>Node icon tone: the view maps it to a theme brush, so view models know nothing about colors.</summary>
public enum DockerTone
{
    Neutral,

    /// <summary>Running: the accent color.</summary>
    Running,
    Warning,
    Error,

    /// <summary>Stopped or empty: muted.</summary>
    Muted,
}
