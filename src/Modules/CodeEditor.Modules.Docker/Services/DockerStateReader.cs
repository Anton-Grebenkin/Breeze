using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.Services;

/// <summary>
/// Docker state for the panel as one snapshot (ADR 0033): containers and images from two parallel processes, workspace
/// compose projects from <see cref="ComposeProjects"/>. Without docker or a responding engine, the snapshot carries
/// only the reason so the panel can say what to do. Read-only; called in the background.
/// </summary>
public sealed class DockerStateReader(DockerRunner docker, ComposeProjects compose, IWorkspace workspace, IFileIndex index)
{
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long to wait for the index of a just-opened folder: compose files are looked up in it.</summary>
    private static readonly TimeSpan IndexWait = TimeSpan.FromSeconds(3);

    private const int ComposeReadersAtOnce = 4;

    public async Task<DockerSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        if (!docker.IsInstalled)
        {
            return DockerSnapshot.Unavailable(DockerAvailability.NotInstalled);
        }

        try
        {
            var reading = RunAsync(DockerQueries.Containers, cancellationToken);
            var imagesReading = RunAsync(DockerQueries.Images, cancellationToken);
            var results = await Task.WhenAll(reading, imagesReading).ConfigureAwait(false);
            var (containers, images) = (results[0], results[1]);
            if ((Problem(containers) ?? Problem(images)) is { } problem)
            {
                return problem;
            }

            return new DockerSnapshot(
                DockerAvailability.Available,
                DockerOutput.Containers(containers.Output),
                DockerOutput.Images(images.Output),
                await ReadComposeAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException)
        {
            // The PATH executable failed to start: removed after the check or not executable.
            return DockerSnapshot.Unavailable(DockerAvailability.NotInstalled);
        }
    }

    private Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        docker.RunForPanelAsync(arguments, ReadTimeout, onLine: null, cancellationToken);

    private static DockerSnapshot? Problem(ProcessResult result) => result switch
    {
        { TimedOut: true } => DockerSnapshot.Unavailable(DockerAvailability.Failed,
            string.Format(CultureInfo.CurrentCulture, Strings.DockerNotResponding, (int)ReadTimeout.TotalSeconds)),
        { ExitCode: 0 } => null,
        _ when DockerErrors.IsEngineStopped(result.Output) => DockerSnapshot.Unavailable(DockerAvailability.EngineStopped),
        _ => DockerSnapshot.Unavailable(DockerAvailability.Failed, DockerErrors.FirstLine(result.Output)),
    };

    private async Task<IReadOnlyList<ComposeProject>> ReadComposeAsync(CancellationToken cancellationToken)
    {
        if (workspace.Root is not { } root)
        {
            return [];
        }

        try
        {
            await index.WhenReady.WaitAsync(IndexWait, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A large folder is still indexing; compose files show up on the next refresh.
        }

        var files = ComposeFileFinder.Find(index.Files);
        var projects = new ComposeProject[files.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = ComposeReadersAtOnce, CancellationToken = cancellationToken };
        await Parallel.ForAsync(0, files.Count, options, async (position, token) =>
            projects[position] = await compose.GetAsync(root, files[position], token).ConfigureAwait(false)).ConfigureAwait(false);
        return projects;
    }
}
