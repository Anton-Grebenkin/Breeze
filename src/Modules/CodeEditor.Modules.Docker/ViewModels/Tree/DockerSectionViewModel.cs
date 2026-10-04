namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>A tree section: "Containers", "Images", "Compose". Expanded initially.</summary>
public sealed class DockerSectionViewModel : DockerNodeViewModel
{
    private readonly string _automationId;

    public DockerSectionViewModel(string key, string title, string automationId)
        : base(key)
    {
        _automationId = automationId;
        Title = title;
        IsExpanded = true;
    }

    public override DockerNodeKind Kind => DockerNodeKind.Section;

    public override string AutomationId => _automationId;

    public void Update(string description)
    {
        Description = description;
        ToolTip = Title;
    }
}
