using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Model;
using CodeEditor.Modules.Docker.ViewModels.Tree;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Modules.Docker.ViewModels;

/// <summary>
/// Resolves what a panel command acts on: the node from the argument (context menu, row button), else the node selected
/// on the visible panel, else a palette pick, like the VS Code Docker extension commands. A hidden panel re-reads state
/// before the pick so the palette list is fresh.
/// </summary>
/// <param name="panel">The panel is created on first show or first command; it does not exist at startup.</param>
public sealed class DockerTargets(Func<DockerViewModel> panel, IQuickPick quickPick, DockerActivity activity)
{
    private DockerViewModel Panel => panel();

    /// <summary>A container or a service's container; the palette offers those that <paramref name="fits"/>.</summary>
    public async Task ContainerAsync(object? argument, Func<DockerContainer, bool> fits, string placeholder, Func<DockerNodeViewModel, DockerContainer, Task> act)
    {
        ArgumentNullException.ThrowIfNull(fits);
        ArgumentNullException.ThrowIfNull(act);
        var node = Node(argument);
        if (DockerViewModel.ContainerOf(node) is { } container && fits(container))
        {
            await act(node!, container);
        }
        else if (argument is not DockerNodeViewModel)
        {
            await PickAsync(() => Panel.Tree.AllContainers.Where(candidate => fits(candidate.Container)), placeholder,
                candidate => (candidate.Title, DockerNodeText.Container(candidate.Container)), candidate => act(candidate, candidate.Container));
        }
    }

    public async Task ImageAsync(object? argument, string placeholder, Func<ImageNodeViewModel, Task> act)
    {
        ArgumentNullException.ThrowIfNull(act);
        if (Node(argument) is ImageNodeViewModel image)
        {
            await act(image);
        }
        else if (argument is not DockerNodeViewModel)
        {
            await PickAsync(() => Panel.Tree.AllImages, placeholder, candidate => (candidate.Title, candidate.Description), act);
        }
    }

    /// <summary>A container or an image, for inspect and copy ID; the palette lists containers, then images.</summary>
    public async Task ContainerOrImageAsync(object? argument, string placeholder, Func<DockerContainer, Task> container, Func<DockerImage, Task> image)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(image);
        var node = Node(argument);
        if (node is ImageNodeViewModel imageNode)
        {
            await image(imageNode.Image);
        }
        else if (DockerViewModel.ContainerOf(node) is { } found)
        {
            await container(found);
        }
        else if (argument is not DockerNodeViewModel)
        {
            await PickAsync(() => Panel.Tree.AllContainers.Cast<DockerNodeViewModel>().Concat(Panel.Tree.AllImages), placeholder,
                candidate => (candidate.Title, candidate.Description),
                candidate => candidate is ImageNodeViewModel picked ? image(picked.Image) : container(DockerViewModel.ContainerOf(candidate)!));
        }
    }

    public async Task ComposeFileAsync(object? argument, string placeholder, Func<ComposeFileNodeViewModel, Task> act)
    {
        ArgumentNullException.ThrowIfNull(act);
        if (Node(argument) is ComposeFileNodeViewModel file)
        {
            await act(file);
        }
        else if (argument is not DockerNodeViewModel)
        {
            await PickAsync(() => Panel.Tree.ComposeFiles, placeholder, candidate => (candidate.Title, candidate.Description), act);
        }
    }

    public async Task ServiceAsync(object? argument, string placeholder, Func<ComposeServiceNodeViewModel, Task> act)
    {
        ArgumentNullException.ThrowIfNull(act);
        if (Node(argument) is ComposeServiceNodeViewModel service)
        {
            await act(service);
        }
        else if (argument is not DockerNodeViewModel)
        {
            await PickAsync(() => Panel.Tree.Services, placeholder, candidate => (candidate.File + ": " + candidate.Title, candidate.Description), act);
        }
    }

    /// <summary>A port from the argument or from the container; several ports mean a palette pick.</summary>
    public Task PortAsync(object? argument, Func<PublishedPort, Task> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        if (argument is PublishedPort port)
        {
            return open(port);
        }

        return ContainerAsync(argument, container => PortLinks.Of(container).Count > 0, Strings.PickContainerWithPorts, (_, container) => ChoosePort(container, open));
    }

    /// <summary>The node from the argument; without one, the node selected on the visible panel.</summary>
    private DockerNodeViewModel? Node(object? argument) => argument switch
    {
        DockerNodeViewModel node => node,
        null when Panel.IsVisible => Panel.Selected,
        _ => null,
    };

    private Task ChoosePort(DockerContainer container, Func<PublishedPort, Task> open)
    {
        var ports = container.Ports.Where(port => port.IsTcp).ToList();
        if (ports.Count == 1)
        {
            return open(ports[0]);
        }

        var items = ports.Select(port => new QuickPickItem(port.Label, port.Url.AbsoluteUri) { Detail = port.Mapping }).ToList();
        quickPick.Show(new QuickPickProvider(Strings.PickPort, items, item => open(ports.First(port => port.Label == item.Id))));
        return Task.CompletedTask;
    }

    private async Task PickAsync<TNode>(
        Func<IEnumerable<TNode>> candidates, string placeholder, Func<TNode, (string Title, string Detail)> describe, Func<TNode, Task> act)
        where TNode : DockerNodeViewModel
    {
        if (!Panel.IsVisible || !Panel.IsAvailable)
        {
            await Panel.RefreshAsync();
        }

        if (!Panel.IsAvailable)
        {
            activity.Failed(Panel.Message);
            return;
        }

        var nodes = candidates().ToList();
        if (nodes.Count == 0)
        {
            activity.Inform(Strings.NothingToPick);
            return;
        }

        var items = nodes.Select(node =>
        {
            var (title, detail) = describe(node);
            return new QuickPickItem(node.Key, title) { Detail = detail };
        }).ToList();
        quickPick.Show(new QuickPickProvider(placeholder, items, item => act(nodes.First(node => node.Key == item.Id))));
    }
}
