using CodeEditor.Modules.Docker.Services.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>A compose service: its container's state, or "not started".</summary>
public sealed partial class ComposeServiceNodeViewModel : DockerNodeViewModel
{
    public ComposeServiceNodeViewModel(string file, string service)
        : base(KeyOf(file, service))
    {
        File = file;
        Service = service;
        Title = service;
        Show(container: null);
    }

    /// <summary>Compose file relative to the workspace root with '/'.</summary>
    public string File { get; }

    public string Service { get; }

    public override DockerNodeKind Kind => DockerNodeKind.ComposeService;

    public override string AutomationId => "Docker.Service." + Service;

    /// <summary>The service's container; <c>null</c> when it never started or its container was removed.</summary>
    [ObservableProperty]
    public partial DockerContainer? Container { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<PublishedPort> Links { get; private set; } = [];

    public static string KeyOf(string file, string service) => "service:" + file + ":" + service;

    public void Update(DockerContainer? container)
    {
        Container = container;
        Show(container);
    }

    private void Show(DockerContainer? container)
    {
        Description = DockerNodeText.Service(container);
        ToolTip = container is null ? Service + "\n" + Description : DockerNodeText.ContainerToolTip(container);
        (Icon, Tone) = container is null ? (DockerIcons.Stopped, DockerTone.Muted) : DockerIcons.Container(container);
        Links = PortLinks.Keep(Links, PortLinks.Of(container));
    }
}
