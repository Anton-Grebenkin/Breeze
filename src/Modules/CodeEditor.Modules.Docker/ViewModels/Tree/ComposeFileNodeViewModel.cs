using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>A workspace compose file: the project and how many services run; its children are the services.</summary>
public sealed partial class ComposeFileNodeViewModel : DockerNodeViewModel
{
    public ComposeFileNodeViewModel(ComposeProject project)
        : base(KeyOf(project.File))
    {
        File = project.File;
        Project = project;
        Title = project.File;
        Icon = DockerIcons.ComposeFile;
        IsExpanded = true;
    }

    /// <summary>Path relative to the workspace root with '/'.</summary>
    public string File { get; }

    public override DockerNodeKind Kind => DockerNodeKind.ComposeFile;

    public override string AutomationId => "Docker.Compose." + File;

    [ObservableProperty]
    public partial ComposeProject Project { get; private set; }

    public static string KeyOf(string file) => "compose:" + file;

    public void Update(ComposeProject project, int running, int total)
    {
        Project = project;
        Description = project.Error is { } error
            ? DockerNodeText.Format(Strings.ComposeReadFailed, error)
            : DockerNodeText.Join(DockerNodeText.Format(Strings.ComposeProjectName, project.Name ?? string.Empty), total > 0 ? DockerNodeText.Running(running, total) : null);
        Tone = project.Error is not null ? DockerTone.Error : running > 0 ? DockerTone.Running : DockerTone.Neutral;
        ToolTip = File + "\n" + Description;
    }
}
