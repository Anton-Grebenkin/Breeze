namespace CodeEditor.Shell.Services;

/// <summary>
/// A request to restart the app (ADR 0011, language change). The window closes the usual way, asking about unsaved
/// files and saving the session, and the entry point starts a new process after exit. If the user cancels closing,
/// the request is withdrawn so the next normal exit doesn't restart.
/// </summary>
public sealed class AppRestart
{
    public bool IsRequested { get; private set; }

    /// <summary>
    /// Starts the new process instead of a plain relaunch, e.g. the installer's updater that applies a downloaded
    /// version first; <c>null</c> — the entry point starts the same executable.
    /// </summary>
    public Action? Relaunch { get; private set; }

    public void Request() => IsRequested = true;

    /// <summary>Sets how the next restart starts the app; <see cref="Request"/> still has to follow.</summary>
    public void RelaunchWith(Action relaunch) => Relaunch = relaunch;

    public void Cancel()
    {
        IsRequested = false;
        Relaunch = null;
    }
}
