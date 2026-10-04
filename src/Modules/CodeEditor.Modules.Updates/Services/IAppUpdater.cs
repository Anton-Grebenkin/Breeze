namespace CodeEditor.Modules.Updates.Services;

/// <summary>
/// The installer's update engine behind an interface, so the update flow is tested without a network or an
/// installation. Calls follow the flow: find, download what was found, apply what was downloaded.
/// </summary>
public interface IAppUpdater
{
    /// <summary><c>false</c> for a build run from the IDE or a build folder: there is nothing to update.</summary>
    bool IsInstalled { get; }

    /// <summary>The version of a newer release, or <c>null</c> when the installed one is the latest.</summary>
    Task<string?> FindUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the release found last, reporting percents.</summary>
    Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the updater, which waits for this process to exit, installs the downloaded release and starts it.
    /// Called by the entry point after the window has closed and the session is saved.
    /// </summary>
    void ApplyAfterExit();
}
