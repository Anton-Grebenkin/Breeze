using CodeEditor.Modules.Updates.Services;

namespace CodeEditor.Modules.Updates.Tests;

/// <summary>An update engine with a preset release; counts calls instead of going to GitHub.</summary>
internal sealed class FakeUpdater : IAppUpdater
{
    public bool IsInstalled { get; set; } = true;

    /// <summary>The newer release; <c>null</c> — the installed version is the latest.</summary>
    public string? Release { get; set; }

    public Exception? FindError { get; set; }

    public Exception? DownloadError { get; set; }

    public int Checks { get; private set; }

    public int Downloads { get; private set; }

    public int Applied { get; private set; }

    public Task<string?> FindUpdateAsync(CancellationToken cancellationToken)
    {
        Checks++;
        return FindError is null ? Task.FromResult(Release) : Task.FromException<string?>(FindError);
    }

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        Downloads++;
        return DownloadError is null ? Task.CompletedTask : Task.FromException(DownloadError);
    }

    public void ApplyAfterExit() => Applied++;
}
