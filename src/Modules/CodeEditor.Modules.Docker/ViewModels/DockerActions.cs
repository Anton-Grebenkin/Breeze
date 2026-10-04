using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Agent;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;
using CodeEditor.Modules.Docker.ViewModels.Tree;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Docker.ViewModels;

/// <summary>
/// Panel actions on Docker (ADR 0033): start, stop, restart and remove a container, remove an image, compose up/down and
/// restart a service. Arguments are as safe as the agent's (<see cref="DockerCommands"/>); removal asks first. While
/// docker runs, the node shows what is happening; progress and results go to <see cref="DockerActivity"/>. Every action
/// raises <see cref="Completed"/> so the panel re-reads state.
/// </summary>
public sealed class DockerActions(DockerRunner docker, DockerActivity activity, IDialogService dialogs) : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>An action finished, successfully or not.</summary>
    public event EventHandler? Completed;

    public Task<bool> StartAsync(DockerNodeViewModel node, DockerContainer container) =>
        RunAsync(node, new DockerOperation(Strings.BusyStarting, Format(Strings.ActionStart, container.Name),
            DockerCommands.Change(new DockerChange(DockerCommands.Start) { Name = container.Id }), DockerRunner.Timeout));

    public Task<bool> StopAsync(DockerNodeViewModel node, DockerContainer container) =>
        RunAsync(node, new DockerOperation(Strings.BusyStopping, Format(Strings.ActionStop, container.Name),
            DockerCommands.Change(new DockerChange(DockerCommands.Stop) { Name = container.Id }), DockerRunner.Timeout));

    public Task<bool> RestartAsync(DockerNodeViewModel node, DockerContainer container) =>
        RunAsync(node, new DockerOperation(Strings.BusyRestarting, Format(Strings.ActionRestart, container.Name),
            DockerCommands.Change(new DockerChange(DockerCommands.Restart) { Name = container.Id }), DockerRunner.Timeout));

    /// <summary>Removes a container after confirmation; a running one is also stopped (<c>--force</c>).</summary>
    public async Task<bool> RemoveAsync(DockerNodeViewModel node, DockerContainer container)
    {
        var question = Format(container.IsRunning ? Strings.RemoveRunningContainerQuestion : Strings.RemoveContainerQuestion, container.Name);
        return dialogs.Confirm(question, Strings.RemoveContainerDetail, Strings.ConfirmRemove)
            && await RunAsync(node, new DockerOperation(Strings.BusyRemoving, Format(Strings.ActionRemove, container.Name),
                DockerQueries.RemoveContainer(container.Id, force: container.IsRunning), DockerRunner.Timeout));
    }

    /// <summary>Removes an image after confirmation; docker refuses (with an error) to remove an image in use.</summary>
    public async Task<bool> RemoveImageAsync(ImageNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var image = node.Image;
        return dialogs.Confirm(Format(Strings.RemoveImageQuestion, image.Title), Strings.RemoveImageDetail, Strings.ConfirmRemove)
            && await RunAsync(node, new DockerOperation(Strings.BusyRemoving, Format(Strings.ActionRemove, image.Title),
                DockerQueries.RemoveImage(image.Reference), DockerRunner.Timeout));
    }

    /// <summary>Starts the project in the background; <paramref name="build"/> rebuilds images first.</summary>
    public Task<bool> ComposeUpAsync(ComposeFileNodeViewModel node, bool build)
    {
        ArgumentNullException.ThrowIfNull(node);
        return RunAsync(node, new DockerOperation(Strings.BusyStarting, Format(build ? Strings.ActionComposeBuild : Strings.ActionComposeUp, node.File),
            DockerCommands.Change(new DockerChange(DockerCommands.ComposeUp) { ComposeFile = node.File, Build = build }), DockerRunner.LongTimeout));
    }

    /// <summary>Starts one project service in the background.</summary>
    public Task<bool> ComposeUpAsync(ComposeServiceNodeViewModel node, bool build)
    {
        ArgumentNullException.ThrowIfNull(node);
        return RunAsync(node, new DockerOperation(Strings.BusyStarting, Format(build ? Strings.ActionComposeBuild : Strings.ActionComposeUp, node.Service),
            DockerCommands.Change(new DockerChange(DockerCommands.ComposeUp) { ComposeFile = node.File, Build = build, Services = [node.Service] }),
            DockerRunner.LongTimeout));
    }

    /// <summary>Stops and removes the project's containers and networks; volumes stay.</summary>
    public Task<bool> ComposeDownAsync(ComposeFileNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return RunAsync(node, new DockerOperation(Strings.BusyStopping, Format(Strings.ActionComposeDown, node.File),
            DockerCommands.Change(new DockerChange(DockerCommands.ComposeDown) { ComposeFile = node.File }), DockerRunner.LongTimeout));
    }

    public Task<bool> RestartServiceAsync(ComposeServiceNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return RunAsync(node, new DockerOperation(Strings.BusyRestarting, Format(Strings.ActionRestart, node.Service),
            DockerQueries.ComposeRestart(node.File, node.Service), DockerRunner.LongTimeout));
    }

    /// <summary>Exiting the app cancels docker, so no processes outlive the window.</summary>
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task<bool> RunAsync(DockerNodeViewModel node, DockerOperation operation)
    {
        node.BusyText = operation.BusyText;
        activity.Started(Format(Strings.ActionProgress, operation.Title), DockerCommands.Display(operation.Arguments));
        try
        {
            var result = await docker.RunForPanelAsync(operation.Arguments, operation.Timeout, activity.Line, _lifetime.Token);
            var error = result.TimedOut ? Format(Strings.ActionTimedOut, (int)operation.Timeout.TotalMinutes)
                : result.ExitCode != 0 ? DockerErrors.FirstLine(result.Output)
                : null;
            Report(operation, error);
            return error is null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or AgentToolException or ArgumentException)
        {
            Report(operation, exception.Message);
            return false;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            node.BusyText = null;
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Report(DockerOperation operation, string? error)
    {
        if (error is null)
        {
            activity.Succeeded(Format(Strings.ActionDone, operation.Title));
        }
        else
        {
            activity.Failed(Format(Strings.ActionFailed, operation.Title, error));
        }
    }

    private static string Format(string format, params object[] arguments) => DockerNodeText.Format(format, arguments);
}
