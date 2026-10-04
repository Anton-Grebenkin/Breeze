using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;
using CodeEditor.Modules.Docker.ViewModels.Tree;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Docker.ViewModels.Tabs;

/// <summary>
/// What the Docker panel opens outside itself: logs and inspect as editor tabs by id (ADR 0031; reopening activates the
/// open tab), the compose file in the editor, a port in the built-in browser (or the default browser without it), and
/// IDs to the clipboard.
/// </summary>
public sealed class DockerTabs(
    IEditorViews editors,
    DockerRunner docker,
    IUiDispatcher dispatcher,
    ISystemShell shell,
    IWebPageOpener pages,
    ICommandService commands,
    IWorkspace workspace,
    DockerActivity activity)
{
    public const string LogsPrefix = "docker.logs:";
    public const string InspectPrefix = "docker.inspect:";

    /// <summary>Logs by container name, so a container recreated by compose with the same name reuses the tab.</summary>
    public void OpenLogs(DockerContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        editors.Open(new EditorViewRequest(LogsPrefix + container.Name, DockerNodeText.Format(Strings.LogsTitle, container.Name), () =>
        {
            var log = new ContainerLogViewModel(container.Name, docker, dispatcher);
            log.Start();
            return log;
        })
        {
            ToolTip = DockerNodeText.Format(Strings.LogsToolTip, container.Name, container.Image),
        });
    }

    public void InspectContainer(DockerContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        Inspect(InspectPrefix + container.Name, container.Name, DockerQueries.InspectContainer(container.Id));
    }

    public void InspectImage(DockerImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Inspect(InspectPrefix + "image:" + image.Reference, image.Title, DockerQueries.InspectImage(image.Reference));
    }

    public void CopyId(string id)
    {
        shell.CopyToClipboard(id);
        activity.Inform(Strings.IdCopied);
    }

    public Task OpenInBrowserAsync(PublishedPort port) => pages.OpenAsync(port.Url);

    public async Task OpenComposeFileAsync(ComposeFileNodeViewModel file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (workspace.Root is { } root)
        {
            await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(Path.GetFullPath(Path.Combine(root, file.File))));
        }
    }

    private void Inspect(string id, string name, IReadOnlyList<string> arguments) =>
        editors.Open(new EditorViewRequest(id, DockerNodeText.Format(Strings.InspectTitle, name), () =>
        {
            var inspect = new InspectViewModel(name, arguments, docker, shell);
            _ = inspect.RefreshAsync();
            return inspect;
        }));
}
