using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Docker.Commands;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services;
using CodeEditor.Modules.Docker.Services.Model;
using CodeEditor.Modules.Docker.ViewModels.Tree;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.ToolWindows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Docker.ViewModels;

/// <summary>
/// The Docker panel (ADR 0033): a tree of containers, images and compose files. State is read in the background while
/// the panel is visible: on show, every <see cref="RefreshInterval"/>, after actions and on Refresh; the UI thread only
/// applies the snapshot to the tree. Without docker or a running engine the panel says what to do. Actions are commands
/// (<see cref="DockerCommandIds"/>), also used by the context menu, row buttons and the palette.
/// </summary>
public sealed partial class DockerViewModel : ObservableObject, IFocusableContent, IDisposable
{
    public const string FocusContextKey = "dockerFocus";

    /// <summary>Kind of the selected node: "container", "image", "composeFile", "composeService".</summary>
    public const string ItemContextKey = "dockerItem";

    /// <summary>The selected node has a container (a container or a compose service with one).</summary>
    public const string HasContainerContextKey = "dockerItemHasContainer";

    public const string RunningContextKey = "dockerItemRunning";

    /// <summary>The selected node's running container publishes a tcp port.</summary>
    public const string PortsContextKey = "dockerItemHasPorts";

    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly DockerStateReader _reader;
    private readonly DockerActions _actions;
    private readonly IWorkspace _workspace;
    private readonly ICommandService _commands;
    private readonly IContextKeyService _context;
    private readonly TimeProvider _time;
    private readonly IUiDispatcher _dispatcher;
    private readonly RefreshLoop _loop;
    private readonly Dictionary<string, MenuViewModel> _menus;

    public DockerViewModel(
        DockerStateReader reader,
        DockerActions actions,
        DockerActivity activity,
        IWorkspace workspace,
        ICommandService commands,
        IContextKeyService context,
        MenuViewModelFactory menus,
        TimeProvider time,
        IUiDispatcher dispatcher)
    {
        _reader = reader;
        _actions = actions;
        _workspace = workspace;
        _commands = commands;
        _context = context;
        _time = time;
        _dispatcher = dispatcher;
        Activity = activity;
        _menus = DockerMenus.MenuIds.ToDictionary(id => id, menus.Create, StringComparer.Ordinal);
        _loop = new RefreshLoop(RefreshInterval, time, dispatcher, RefreshCoreAsync);
        _workspace.Changed += OnWorkspaceChanged;
        _actions.Completed += OnActionCompleted;
    }

    public DockerTree Tree { get; } = new();

    public DockerActivity Activity { get; }

    /// <summary>Context menu of the selected node; each node kind has its own.</summary>
    public MenuViewModel ContextMenu => _menus[DockerMenus.MenuOf(Selected?.Kind)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable), nameof(IsLoading), nameof(IsUnavailable))]
    public partial DockerAvailability Availability { get; private set; }

    /// <summary>Why Docker is unavailable and what to do.</summary>
    [ObservableProperty]
    public partial string Message { get; private set; } = Strings.Loading;

    [ObservableProperty]
    public partial DockerNodeViewModel? Selected { get; set; }

    public bool IsAvailable => Availability == DockerAvailability.Available;

    public bool IsLoading => Availability == DockerAvailability.Unknown;

    public bool IsUnavailable => !IsAvailable && !IsLoading;

    /// <summary>The panel is on screen; only then do commands use its selection and state refresh on its own.</summary>
    public bool IsVisible => _loop.IsActive;

    public event EventHandler? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The panel was shown or hidden (another panel, collapsed area).</summary>
    /// <returns>On show, the initial refresh.</returns>
    public Task SetVisible(bool visible)
    {
        if (visible)
        {
            return _loop.ActivateAsync();
        }

        _loop.Deactivate();
        return Task.CompletedTask;
    }

    public void SetFocused(bool focused) => _context.Set(FocusContextKey, focused);

    /// <summary>Re-reads state now; the task completes when the tree is fresh.</summary>
    public Task RefreshAsync() => _loop.RefreshAsync();

    /// <summary>Called before the context menu opens: the selected node's keys may be stale after a refresh.</summary>
    public void PrepareContextMenu()
    {
        UpdateSelectionContext();
        ContextMenu.Refresh();
    }

    /// <summary>The node's container: the container itself or a compose service's container.</summary>
    public static DockerContainer? ContainerOf(DockerNodeViewModel? node) => node switch
    {
        ContainerNodeViewModel container => container.Container,
        ComposeServiceNodeViewModel service => service.Container,
        _ => null,
    };

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _actions.Completed -= OnActionCompleted;
        _loop.Dispose();
        foreach (var menu in _menus.Values)
        {
            menu.Dispose();
        }
    }

    [RelayCommand]
    private Task RefreshPanel() => Run(DockerCommandIds.Refresh, argument: null);

    [RelayCommand]
    private Task Start(DockerNodeViewModel? node) => Run(DockerCommandIds.Start, node);

    [RelayCommand]
    private Task Stop(DockerNodeViewModel? node) => Run(DockerCommandIds.Stop, node);

    [RelayCommand]
    private Task Restart(DockerNodeViewModel? node) => Run(DockerCommandIds.Restart, node);

    [RelayCommand]
    private Task Logs(DockerNodeViewModel? node) => Run(DockerCommandIds.Logs, node);

    [RelayCommand]
    private Task ComposeUp(DockerNodeViewModel? node) => Run(DockerCommandIds.ComposeUp, node);

    [RelayCommand]
    private Task ComposeDown(DockerNodeViewModel? node) => Run(DockerCommandIds.ComposeDown, node);

    [RelayCommand]
    private Task RestartService(DockerNodeViewModel? node) => Run(DockerCommandIds.ComposeRestart, node);

    /// <summary>Double click: container or service logs, image details, the compose file.</summary>
    [RelayCommand]
    private Task Open(DockerNodeViewModel? node) => node switch
    {
        ImageNodeViewModel => Run(DockerCommandIds.Inspect, node),
        ComposeFileNodeViewModel => Run(DockerCommandIds.OpenComposeFile, node),
        _ when ContainerOf(node) is not null => Run(DockerCommandIds.Logs, node),
        _ => Task.CompletedTask,
    };

    /// <summary>The port link in a container row.</summary>
    [RelayCommand]
    private Task OpenPort(PublishedPort port) => Run(DockerCommandIds.OpenInBrowser, port);

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in Tree.Sections.SelectMany(section => section.Children))
        {
            node.IsExpanded = false;
        }
    }

    partial void OnSelectedChanged(DockerNodeViewModel? value)
    {
        OnPropertyChanged(nameof(ContextMenu));
        UpdateSelectionContext();
    }

    private async Task Run(string commandId, object? argument) => await _commands.ExecuteAsync(commandId, argument);

    // docker runs in the background; the snapshot is applied on the UI thread because the tree is bound to the view.
    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var snapshot = await Task.Run(() => _reader.ReadAsync(cancellationToken), cancellationToken);
        await _dispatcher.InvokeAsync(() => Apply(snapshot));
    }

    private void Apply(DockerSnapshot snapshot)
    {
        Message = snapshot.Availability switch
        {
            DockerAvailability.NotInstalled => Strings.NotInstalledMessage,
            DockerAvailability.EngineStopped => Strings.EngineStoppedMessage,
            DockerAvailability.Failed => DockerNodeText.Format(Strings.FailedMessage, snapshot.Error ?? string.Empty),
            _ => string.Empty,
        };
        if (snapshot.Availability == DockerAvailability.Available)
        {
            Tree.Apply(snapshot, _workspace.Root is not null, _time.GetUtcNow());
        }

        // A removed node does not stay selected, so argument-less commands cannot act on it.
        if (Selected is { } selected && !Tree.Contains(selected))
        {
            Selected = null;
        }

        Availability = snapshot.Availability;
        UpdateSelectionContext();
    }

    private void UpdateSelectionContext()
    {
        var container = ContainerOf(Selected);
        _context.Set(ItemContextKey, Selected is { } node ? DockerMenus.ContextValue(node.Kind) : null);
        _context.Set(HasContainerContextKey, container is not null);
        _context.Set(RunningContextKey, container?.IsRunning ?? false);
        _context.Set(PortsContextKey, PortLinks.Of(container).Count > 0);
    }

    // Show the state after an action at once, not after the refresh interval.
    private void OnActionCompleted(object? sender, EventArgs e) => _ = RefreshAsync();

    // Another folder means other compose files.
    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        if (IsVisible)
        {
            _ = RefreshAsync();
        }
    }
}
