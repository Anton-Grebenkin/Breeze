using CodeEditor.Modules.Docker.Resources;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>Containers of one compose project (label <c>com.docker.compose.project</c>), grouped as in VS Code.</summary>
public sealed class ContainerGroupViewModel : DockerNodeViewModel
{
    public ContainerGroupViewModel(string project)
        : base(KeyOf(project))
    {
        Project = project;
        Title = project;
        Icon = DockerIcons.Project;
    }

    public string Project { get; }

    public override DockerNodeKind Kind => DockerNodeKind.ContainerGroup;

    public override string AutomationId => "Docker.Project." + Project;

    public static string KeyOf(string project) => "project:" + project;

    public void Update(int running, int total)
    {
        Description = DockerNodeText.Running(running, total);
        Tone = running > 0 ? DockerTone.Running : DockerTone.Muted;
        ToolTip = DockerNodeText.Format(Strings.ToolTipProject, Project) + "\n" + Description;
    }
}
