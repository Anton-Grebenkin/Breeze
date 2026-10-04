using CodeEditor.Modules.Docker.Services.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>A container: name, state as icon and text, image, links to published ports.</summary>
public sealed partial class ContainerNodeViewModel : DockerNodeViewModel
{
    public ContainerNodeViewModel(DockerContainer container)
        : base(KeyOf(container.Id))
    {
        Container = container;
        Show(container);
    }

    public override DockerNodeKind Kind => DockerNodeKind.Container;

    public override string AutomationId => "Docker.Container." + Container.Name;

    [ObservableProperty]
    public partial DockerContainer Container { get; private set; }

    /// <summary>Port links in the row (<see cref="PortLinks"/>).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PublishedPort> Links { get; private set; } = [];

    public static string KeyOf(string id) => "container:" + id;

    public void Update(DockerContainer container)
    {
        Container = container;
        Show(container);
    }

    private void Show(DockerContainer container)
    {
        Title = container.Name;
        Description = DockerNodeText.Container(container);
        ToolTip = DockerNodeText.ContainerToolTip(container);
        (Icon, Tone) = DockerIcons.Container(container);
        Links = PortLinks.Keep(Links, PortLinks.Of(container));
    }
}
