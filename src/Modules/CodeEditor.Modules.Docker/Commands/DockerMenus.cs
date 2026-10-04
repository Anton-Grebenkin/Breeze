using CodeEditor.Core.Context;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.ViewModels;
using CodeEditor.Modules.Docker.ViewModels.Tree;

namespace CodeEditor.Modules.Docker.Commands;

/// <summary>
/// Docker tree context menus, one per node kind, so menu groups never empty out and separators never double. Items
/// depend on container state ("Stop" when running, "Start" when stopped), as in the VS Code Docker extension.
/// </summary>
public static class DockerMenus
{
    /// <summary>A node without actions (section, group) or no selection: only "Refresh".</summary>
    public const string GeneralMenuId = "docker.context";
    public const string ContainerMenuId = "docker.context.container";
    public const string ImageMenuId = "docker.context.image";
    public const string ComposeFileMenuId = "docker.context.composeFile";
    public const string ServiceMenuId = "docker.context.composeService";

    private const string OpenGroup = "1_open";
    private const string StateGroup = "2_state";
    private const string CopyGroup = "3_copy";
    private const string RemoveGroup = "4_remove";
    private const string RefreshGroup = "9_refresh";

    public static IReadOnlyList<string> MenuIds { get; } = [GeneralMenuId, ContainerMenuId, ImageMenuId, ComposeFileMenuId, ServiceMenuId];

    /// <summary>Value of the <c>dockerItem</c> context key for a node kind.</summary>
    public static string ContextValue(DockerNodeKind kind) => kind switch
    {
        DockerNodeKind.Section => "section",
        DockerNodeKind.ContainerGroup => "containerGroup",
        DockerNodeKind.Container => "container",
        DockerNodeKind.Image => "image",
        DockerNodeKind.ComposeFile => "composeFile",
        DockerNodeKind.ComposeService => "composeService",
        _ => "hint",
    };

    public static string MenuOf(DockerNodeKind? kind) => kind switch
    {
        DockerNodeKind.Container => ContainerMenuId,
        DockerNodeKind.Image => ImageMenuId,
        DockerNodeKind.ComposeFile => ComposeFileMenuId,
        DockerNodeKind.ComposeService => ServiceMenuId,
        _ => GeneralMenuId,
    };

    internal static IEnumerable<MenuItemDefinition> ContextItems()
    {
        var running = DockerViewModel.RunningContextKey;
        var ports = DockerViewModel.PortsContextKey;
        var hasContainer = DockerViewModel.HasContainerContextKey;
        (string Menu, string Command, string Group, string Title, string? When)[] items =
        [
            (ContainerMenuId, DockerCommandIds.Logs, OpenGroup, Strings.MenuLogs, null),
            (ContainerMenuId, DockerCommandIds.Inspect, OpenGroup, Strings.MenuInspect, null),
            (ContainerMenuId, DockerCommandIds.OpenInBrowser, OpenGroup, Strings.MenuOpenInBrowser, ports),
            (ContainerMenuId, DockerCommandIds.Start, StateGroup, Strings.MenuStart, "!" + running),
            (ContainerMenuId, DockerCommandIds.Stop, StateGroup, Strings.MenuStop, running),
            (ContainerMenuId, DockerCommandIds.Restart, StateGroup, Strings.MenuRestart, running),
            (ContainerMenuId, DockerCommandIds.CopyId, CopyGroup, Strings.MenuCopyId, null),
            (ContainerMenuId, DockerCommandIds.Remove, RemoveGroup, Strings.MenuRemove, null),
            (ImageMenuId, DockerCommandIds.Inspect, OpenGroup, Strings.MenuInspect, null),
            (ImageMenuId, DockerCommandIds.CopyId, CopyGroup, Strings.MenuCopyId, null),
            (ImageMenuId, DockerCommandIds.RemoveImage, RemoveGroup, Strings.MenuRemoveImage, null),
            (ComposeFileMenuId, DockerCommandIds.OpenComposeFile, OpenGroup, Strings.MenuOpenComposeFile, null),
            (ComposeFileMenuId, DockerCommandIds.ComposeUp, StateGroup, Strings.MenuComposeUp, null),
            (ComposeFileMenuId, DockerCommandIds.ComposeUpBuild, StateGroup, Strings.MenuComposeUpBuild, null),
            (ComposeFileMenuId, DockerCommandIds.ComposeDown, StateGroup, Strings.MenuComposeDown, null),
            (ServiceMenuId, DockerCommandIds.ComposeUp, StateGroup, Strings.MenuComposeUpService, null),
            (ServiceMenuId, DockerCommandIds.ComposeUpBuild, StateGroup, Strings.MenuComposeUpBuild, null),
            (ServiceMenuId, DockerCommandIds.ComposeRestart, StateGroup, Strings.MenuComposeRestart, null),
            (ServiceMenuId, DockerCommandIds.Stop, StateGroup, Strings.MenuStop, running),
            (ServiceMenuId, DockerCommandIds.Logs, StateGroup, Strings.MenuLogs, hasContainer),
            (ServiceMenuId, DockerCommandIds.Inspect, StateGroup, Strings.MenuInspect, hasContainer),
            (ServiceMenuId, DockerCommandIds.OpenInBrowser, StateGroup, Strings.MenuOpenInBrowser, ports),
        ];

        var refresh = MenuIds.Select(menu => MenuItemDefinition.ForCommand(menu, DockerCommandIds.Refresh, RefreshGroup, order: 0, Strings.MenuRefresh));
        return items.Select((entry, order) => MenuItemDefinition.ForCommand(
                entry.Menu, entry.Command, entry.Group, order, entry.Title, entry.When is null ? null : ContextExpression.Parse(entry.When)))
            .Concat(refresh);
    }
}
