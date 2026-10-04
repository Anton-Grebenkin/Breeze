using CodeEditor.Shell.Services;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace CodeEditor.Modules.Updates.Services;

/// <summary>
/// Velopack updates from the GitHub releases of the repository the build came from: a pre-release build also takes
/// pre-releases, a stable one only stable releases. A build without a repository (local) or not installed by
/// Setup.exe has nothing to update, as has a host that skipped <c>VelopackApp.Run()</c> in its entry point (tests).
/// Network and file work runs off the UI thread.
/// </summary>
public sealed class VelopackUpdater(ProductInfo product) : IAppUpdater
{
    private readonly Lazy<UpdateManager?> _manager = new(() => Create(product));
    private UpdateInfo? _found;

    public bool IsInstalled => _manager.Value?.IsInstalled == true;

    public async Task<string?> FindUpdateAsync(CancellationToken cancellationToken)
    {
        var manager = Installed();
        _found = await Task.Run(manager.CheckForUpdatesAsync, cancellationToken).ConfigureAwait(false);
        return _found?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var manager = Installed();
        var found = _found ?? throw new InvalidOperationException("No update has been found to download");
        return Task.Run(() => manager.DownloadUpdatesAsync(found, progress.Report, cancellationToken), cancellationToken);
    }

    public void ApplyAfterExit() =>
        Installed().WaitExitThenApplyUpdates(_found?.TargetFullRelease, silent: false, restart: true);

    private UpdateManager Installed() =>
        _manager.Value is { IsInstalled: true } manager ? manager : throw new InvalidOperationException("The app is not installed");

    private static UpdateManager? Create(ProductInfo product) =>
        product.Repository is { } repository && VelopackLocator.IsCurrentSet
            ? new UpdateManager(new GithubSource(repository.AbsoluteUri, accessToken: null, prerelease: product.IsPrerelease))
            : null;
}
