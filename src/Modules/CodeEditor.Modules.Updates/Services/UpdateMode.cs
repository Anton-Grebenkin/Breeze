namespace CodeEditor.Modules.Updates.Services;

/// <summary>When Breeze looks for updates: the <c>update.mode</c> setting.</summary>
public enum UpdateMode
{
    /// <summary>Shortly after startup, downloading in the background; the update installs on restart.</summary>
    Default,

    /// <summary>Only by the "Check for Updates…" command.</summary>
    Manual,
}
