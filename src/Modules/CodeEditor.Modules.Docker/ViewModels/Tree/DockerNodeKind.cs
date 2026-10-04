namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>
/// Kind of a Docker tree node. The selected node's value is the <c>dockerItem</c> context key, which picks the node's
/// context menu actions.
/// </summary>
public enum DockerNodeKind
{
    Section,
    ContainerGroup,
    Container,
    Image,
    ComposeFile,
    ComposeService,
    Hint,
}
