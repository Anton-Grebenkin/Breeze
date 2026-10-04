using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Model;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>
/// The Docker panel tree: containers (by compose project, then standalone), images, workspace compose files with their
/// services. A snapshot is applied to the existing nodes (<see cref="NodeList"/>), so the tree does not flicker and the
/// selection stays. Ordered by name, not state, so a row does not jump when a container stops. UI thread only.
/// </summary>
public sealed class DockerTree
{
    public DockerTree()
    {
        Containers = new DockerSectionViewModel("section:containers", Strings.SectionContainers, "Docker.Section.Containers");
        Images = new DockerSectionViewModel("section:images", Strings.SectionImages, "Docker.Section.Images");
        Compose = new DockerSectionViewModel("section:compose", Strings.SectionCompose, "Docker.Section.Compose");
        Sections = [Containers, Images, Compose];
    }

    public DockerSectionViewModel Containers { get; }

    public DockerSectionViewModel Images { get; }

    public DockerSectionViewModel Compose { get; }

    public IReadOnlyList<DockerSectionViewModel> Sections { get; }

    /// <summary>All containers, including those inside project groups.</summary>
    public IEnumerable<ContainerNodeViewModel> AllContainers =>
        Containers.Children.SelectMany(node => node is ContainerGroupViewModel group ? (IEnumerable<DockerNodeViewModel>)group.Children : [node]).OfType<ContainerNodeViewModel>();

    public IEnumerable<ImageNodeViewModel> AllImages => Images.Children.OfType<ImageNodeViewModel>();

    public IEnumerable<ComposeFileNodeViewModel> ComposeFiles => Compose.Children.OfType<ComposeFileNodeViewModel>();

    public IEnumerable<ComposeServiceNodeViewModel> Services => ComposeFiles.SelectMany(file => file.Children.OfType<ComposeServiceNodeViewModel>());

    /// <summary>Whether the node is still in the tree.</summary>
    /// <remarks>O(n) over tree nodes; there are hundreds at most.</remarks>
    public bool Contains(DockerNodeViewModel node) => Sections.Any(section => ReferenceEquals(section, node) || Contains(section.Children, node));

    /// <param name="hasWorkspace">A folder is open; without one the compose section suggests opening a folder.</param>
    /// <param name="now">For image age.</param>
    public void Apply(DockerSnapshot snapshot, bool hasWorkspace, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ApplyContainers(snapshot.Containers);
        ApplyImages(snapshot.Images, now);
        ApplyCompose(snapshot.Compose, snapshot.Containers, hasWorkspace);
    }

    private void ApplyContainers(IReadOnlyList<DockerContainer> containers)
    {
        var existing = NodeList.ByKey(Containers.Children);
        var desired = new List<DockerNodeViewModel>();
        foreach (var project in containers.Where(container => container.Project is not null)
            .GroupBy(container => container.Project!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, NaturalStringComparer.Instance))
        {
            desired.Add(Group(existing, project.Key, [.. project]));
        }

        desired.AddRange(ByName(containers.Where(container => container.Project is null)).Select(container => Container(existing, container)));
        if (desired.Count == 0)
        {
            desired.Add(Hint(existing, "hint:containers", Strings.NoContainers));
        }

        NodeList.Apply(Containers.Children, desired);
        Containers.Update(containers.Count > 0 ? DockerNodeText.Running(containers.Count(container => container.IsRunning), containers.Count) : string.Empty);
    }

    private ContainerGroupViewModel Group(Dictionary<string, DockerNodeViewModel> existing, string project, IReadOnlyList<DockerContainer> containers)
    {
        var running = containers.Count(container => container.IsRunning);
        var group = NodeList.Reuse(existing, ContainerGroupViewModel.KeyOf(project), () => new ContainerGroupViewModel(project) { IsExpanded = running > 0 });
        group.Update(running, containers.Count);
        var children = NodeList.ByKey(group.Children);
        NodeList.Apply(group.Children, [.. ByName(containers).Select(container => Container(children, container))]);
        return group;
    }

    private static ContainerNodeViewModel Container(Dictionary<string, DockerNodeViewModel> existing, DockerContainer container)
    {
        var node = NodeList.Reuse(existing, ContainerNodeViewModel.KeyOf(container.Id), () => new ContainerNodeViewModel(container));
        node.Update(container);
        return node;
    }

    private void ApplyImages(IReadOnlyList<DockerImage> images, DateTimeOffset now)
    {
        var existing = NodeList.ByKey(Images.Children);
        var desired = new List<DockerNodeViewModel>();
        foreach (var image in images.OrderBy(image => image.IsDangling).ThenBy(image => image.Title, NaturalStringComparer.Instance))
        {
            var node = NodeList.Reuse(existing, ImageNodeViewModel.KeyOf(image), () => new ImageNodeViewModel(image, now));
            node.Update(image, now);
            desired.Add(node);
        }

        if (desired.Count == 0)
        {
            desired.Add(Hint(existing, "hint:images", Strings.NoImages));
        }

        NodeList.Apply(Images.Children, desired);
        Images.Update(images.Count > 0 ? images.Count.ToString(CultureInfo.CurrentCulture) : string.Empty);
    }

    private void ApplyCompose(IReadOnlyList<ComposeProject> projects, IReadOnlyList<DockerContainer> containers, bool hasWorkspace)
    {
        var existing = NodeList.ByKey(Compose.Children);
        var desired = new List<DockerNodeViewModel>();
        foreach (var project in projects)
        {
            var node = NodeList.Reuse(existing, ComposeFileNodeViewModel.KeyOf(project.File), () => new ComposeFileNodeViewModel(project));
            DockerContainer[] own = project.Name is { } name ? [.. containers.Where(container => container.Project == name)] : [];
            node.Update(project, own.Count(container => container.IsRunning), own.Length);
            var services = NodeList.ByKey(node.Children);
            NodeList.Apply(node.Children, [.. project.Services.Select(service => Service(services, project.File, service, own))]);
            desired.Add(node);
        }

        if (desired.Count == 0)
        {
            desired.Add(Hint(existing, "hint:compose", hasWorkspace ? Strings.NoComposeFiles : Strings.OpenFolderForCompose));
        }

        NodeList.Apply(Compose.Children, desired);
        Compose.Update(projects.Count > 0 ? projects.Count.ToString(CultureInfo.CurrentCulture) : string.Empty);
    }

    // For a scaled service with several instances, a running one is shown.
    private static ComposeServiceNodeViewModel Service(
        Dictionary<string, DockerNodeViewModel> existing, string file, string service, IReadOnlyList<DockerContainer> containers)
    {
        var node = NodeList.Reuse(existing, ComposeServiceNodeViewModel.KeyOf(file, service), () => new ComposeServiceNodeViewModel(file, service));
        node.Update(containers.Where(container => container.Service == service).OrderByDescending(container => container.IsRunning).FirstOrDefault());
        return node;
    }

    private static DockerHintViewModel Hint(Dictionary<string, DockerNodeViewModel> existing, string key, string text)
    {
        var hint = NodeList.Reuse(existing, key, () => new DockerHintViewModel(key, text));
        hint.Update(text);
        return hint;
    }

    private static bool Contains(IEnumerable<DockerNodeViewModel> nodes, DockerNodeViewModel node) =>
        nodes.Any(candidate => ReferenceEquals(candidate, node) || Contains(candidate.Children, node));

    private static IEnumerable<DockerContainer> ByName(IEnumerable<DockerContainer> containers) =>
        containers.OrderBy(container => container.Name, NaturalStringComparer.Instance);
}
