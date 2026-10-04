namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>A hint in place of an empty section: "No containers", "No compose files in the folder".</summary>
public sealed class DockerHintViewModel : DockerNodeViewModel
{
    public DockerHintViewModel(string key, string text)
        : base(key)
    {
        Title = text;
        ToolTip = text;
        Icon = DockerIcons.Hint;
        Tone = DockerTone.Muted;
    }

    public override DockerNodeKind Kind => DockerNodeKind.Hint;

    public override string AutomationId => "Docker.Hint";

    public void Update(string text)
    {
        Title = text;
        ToolTip = text;
    }
}
