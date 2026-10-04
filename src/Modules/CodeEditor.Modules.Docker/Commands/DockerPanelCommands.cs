using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.ViewModels;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using CodeEditor.Modules.Docker.ViewModels.Tree;

namespace CodeEditor.Modules.Docker.Commands;

/// <summary>
/// Docker panel commands (ADR 0033), used by the palette ("Docker: Stop Container"), the tree context menu and row
/// buttons. Tree keys: Enter opens logs, image details or the compose file, Delete removes, Ctrl+C copies the ID.
/// Without a node a command takes the one selected on the visible panel, otherwise asks in the palette
/// (<see cref="DockerTargets"/>).
/// </summary>
/// <param name="panel">The panel is created on first show or first command.</param>
public sealed class DockerPanelCommands(Func<DockerViewModel> panel, DockerTargets targets, DockerActions actions, DockerTabs tabs) : IDisposable
{
    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(menus);
        RegisterContainerCommands(commands);
        RegisterComposeCommands(commands);
        Add(commands, DockerCommandIds.Refresh, Strings.CommandRefresh, _ => panel().RefreshAsync());
        Add(commands, DockerCommandIds.RemoveImage, Strings.CommandRemoveImage,
            argument => targets.ImageAsync(argument, Strings.PickImage, actions.RemoveImageAsync));
        Add(commands, DockerCommandIds.Inspect, Strings.CommandInspect, argument => targets.ContainerOrImageAsync(argument, Strings.PickContainerOrImage,
            container => Done(() => tabs.InspectContainer(container)), image => Done(() => tabs.InspectImage(image))));
        Add(commands, DockerCommandIds.CopyId, Strings.CommandCopyId, argument => targets.ContainerOrImageAsync(argument, Strings.PickContainerOrImage,
            container => Done(() => tabs.CopyId(container.Id)), image => Done(() => tabs.CopyId(image.Id))));
        BindKeys(keybindings);
        _registrations.AddRange(DockerMenus.ContextItems().Select(menus.Register));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void RegisterContainerCommands(ICommandRegistry commands)
    {
        Add(commands, DockerCommandIds.Start, Strings.CommandStart,
            argument => targets.ContainerAsync(argument, container => !container.IsRunning, Strings.PickStoppedContainer, actions.StartAsync));
        Add(commands, DockerCommandIds.Stop, Strings.CommandStop,
            argument => targets.ContainerAsync(argument, container => container.IsRunning, Strings.PickRunningContainer, actions.StopAsync));
        Add(commands, DockerCommandIds.Restart, Strings.CommandRestart,
            argument => targets.ContainerAsync(argument, container => container.IsRunning, Strings.PickRunningContainer, actions.RestartAsync));
        Add(commands, DockerCommandIds.Remove, Strings.CommandRemove,
            argument => targets.ContainerAsync(argument, _ => true, Strings.PickContainer, actions.RemoveAsync));
        Add(commands, DockerCommandIds.Logs, Strings.CommandLogs,
            argument => targets.ContainerAsync(argument, _ => true, Strings.PickContainer, (_, container) => Done(() => tabs.OpenLogs(container))));
        Add(commands, DockerCommandIds.OpenInBrowser, Strings.CommandOpenInBrowser, argument => targets.PortAsync(argument, tabs.OpenInBrowserAsync));
    }

    // compose needs an open folder: compose files come from it.
    private void RegisterComposeCommands(ICommandRegistry commands)
    {
        var workspaceOpen = ContextExpression.Parse(IWorkspace.OpenContextKey);
        Add(commands, DockerCommandIds.ComposeUp, Strings.CommandComposeUp, argument => ComposeUpAsync(argument, build: false), workspaceOpen);
        Add(commands, DockerCommandIds.ComposeUpBuild, Strings.CommandComposeUpBuild, argument => ComposeUpAsync(argument, build: true), workspaceOpen);
        Add(commands, DockerCommandIds.ComposeDown, Strings.CommandComposeDown,
            argument => targets.ComposeFileAsync(argument, Strings.PickComposeFile, actions.ComposeDownAsync), workspaceOpen);
        Add(commands, DockerCommandIds.ComposeRestart, Strings.CommandComposeRestart,
            argument => targets.ServiceAsync(argument, Strings.PickService, actions.RestartServiceAsync), workspaceOpen);
        Add(commands, DockerCommandIds.OpenComposeFile, Strings.CommandOpenComposeFile,
            argument => targets.ComposeFileAsync(argument, Strings.PickComposeFile, tabs.OpenComposeFileAsync), workspaceOpen);
    }

    // A service starts alone, a file starts the whole project; without a node, pick a file.
    private Task ComposeUpAsync(object? argument, bool build) =>
        (argument ?? (panel().IsVisible ? panel().Selected : null)) is ComposeServiceNodeViewModel service
            ? actions.ComposeUpAsync(service, build)
            : targets.ComposeFileAsync(argument, Strings.PickComposeFile, file => actions.ComposeUpAsync(file, build));

    private void BindKeys(IKeybindingRegistry keybindings)
    {
        var focus = DockerViewModel.FocusContextKey;
        var item = DockerViewModel.ItemContextKey;
        var hasContainer = DockerViewModel.HasContainerContextKey;
        Bind(keybindings, "Enter", DockerCommandIds.Logs, $"{focus} && {hasContainer}");
        Bind(keybindings, "Enter", DockerCommandIds.OpenComposeFile, $"{focus} && {item} == composeFile");
        Bind(keybindings, "Enter", DockerCommandIds.Inspect, $"{focus} && {item} == image");
        Bind(keybindings, "Delete", DockerCommandIds.Remove, $"{focus} && {item} == container");
        Bind(keybindings, "Delete", DockerCommandIds.RemoveImage, $"{focus} && {item} == image");
        Bind(keybindings, "Ctrl+C", DockerCommandIds.CopyId, $"{focus} && ({hasContainer} || {item} == image)");
    }

    private void Add(ICommandRegistry commands, string id, string title, Func<object?, Task> handler, ContextExpression? when = null) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (argument, _) => await handler(argument), Strings.ModuleName, when)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId, string when) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, ContextExpression.Parse(when))));

    private static Task Done(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
