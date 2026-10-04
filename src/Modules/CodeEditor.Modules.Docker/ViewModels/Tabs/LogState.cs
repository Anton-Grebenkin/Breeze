namespace CodeEditor.Modules.Docker.ViewModels.Tabs;

/// <summary>State of a container log stream.</summary>
public enum LogState
{
    /// <summary>docker returns the last lines and then new ones as they appear.</summary>
    Live,

    /// <summary>docker finished: the container stopped.</summary>
    Ended,

    /// <summary>docker returned an error: no such container, or the engine is not running.</summary>
    Failed,
}
