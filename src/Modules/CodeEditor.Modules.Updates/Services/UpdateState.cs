namespace CodeEditor.Modules.Updates.Services;

/// <summary>Where the update flow is: one check or download at a time.</summary>
public enum UpdateState
{
    Idle,
    Checking,
    Downloading,

    /// <summary>Downloaded: a restart installs it, otherwise the next start does.</summary>
    Ready,
}
