using System.Collections.Concurrent;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.Services;

/// <summary>
/// Compose projects of workspace files, cached by write time. docker reads a file (<c>compose config</c>, about half a
/// second per process) on first show and after the file changes, so periodic panel refreshes do not spawn a process per
/// file.
/// </summary>
public sealed class ComposeProjects(DockerRunner docker, IFileSystem fileSystem)
{
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (DateTime Written, ComposeProject Project)> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="root">Workspace root.</param>
    /// <param name="file">File relative to the root with '/'.</param>
    public async Task<ComposeProject> GetAsync(string root, string file, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(root, file);
        var written = LastWrite(fullPath);
        if (_cache.TryGetValue(fullPath, out var cached) && cached.Written == written)
        {
            return cached.Project;
        }

        var project = await ReadAsync(file, cancellationToken).ConfigureAwait(false);
        _cache[fullPath] = (written, project);
        return project;
    }

    private async Task<ComposeProject> ReadAsync(string file, CancellationToken cancellationToken)
    {
        var result = await docker.RunForPanelAsync(DockerQueries.ComposeConfig(file), ReadTimeout, onLine: null, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && !result.TimedOut
            ? DockerOutput.Compose(file, result.Output)
            : ComposeProject.Failed(file, DockerErrors.FirstLine(result.Output));
    }

    // If the file was deleted after indexing, use the default time; docker itself reports the missing file.
    private DateTime LastWrite(string path)
    {
        try
        {
            return fileSystem.GetLastWriteTimeUtc(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }
}
