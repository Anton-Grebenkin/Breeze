using System.Text.Json;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Docker.Commands;
using CodeEditor.Modules.Docker.Services;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.ViewModels;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using CodeEditor.Modules.Docker.ViewModels.Tree;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// The whole Docker panel over a scripted docker (<see cref="ScriptedDocker"/>): state reading, tree, commands with the
/// context menu and keys, tabs. The folder is C:\app; docker "exists" in C:\tools. Time is manual.
/// </summary>
internal sealed class PanelFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\app");
    private static readonly string ToolsFolder = Path.GetFullPath(@"C:\tools");

    public PanelFixture(bool dockerInstalled = true)
    {
        Files = new FakeFileSystem().AddDirectory(Root);
        if (dockerInstalled)
        {
            Files.AddFile(Path.Combine(ToolsFolder, "docker.exe"));
        }

        Workspace = new Workspace(Files, Context, NullLogger<Workspace>.Instance);
        CommandService = new CommandService(Commands, Context, NullLogger<CommandService>.Instance);
        Runner = new DockerRunner(Docker, Workspace, Files, ToolsFolder, ".EXE");
        Activity = new DockerActivity(StatusBar, Output);
        Actions = new DockerActions(Runner, Activity, Dialogs);
        var menus = new MenuViewModelFactory(new MenuBuilder(Menus, Commands, Keybindings, CommandService, Context), Menus, Commands, Keybindings);
        var reader = new DockerStateReader(Runner, new ComposeProjects(Runner, Files), Workspace, Index);
        Panel = new DockerViewModel(reader, Actions, Activity, Workspace, CommandService, Context, menus, Time, new InlineUiDispatcher());
        Tabs = new DockerTabs(EditorViews, Runner, new InlineUiDispatcher(), SystemShell, new SystemWebPageOpener(SystemShell), CommandService, Workspace, Activity);
        PanelCommands = new DockerPanelCommands(() => Panel, new DockerTargets(() => Panel, QuickPick, Activity), Actions, Tabs);
        PanelCommands.Register(Commands, Keybindings, Menus);
    }

    public ScriptedDocker Docker { get; } = new();

    public FakeFileSystem Files { get; }

    public StaticFileIndex Index { get; } = new();

    public ContextKeyService Context { get; } = new();

    public CommandRegistry Commands { get; } = new();

    public KeybindingRegistry Keybindings { get; } = new();

    public MenuRegistry Menus { get; } = new();

    public CommandService CommandService { get; }

    public Workspace Workspace { get; }

    public DockerRunner Runner { get; }

    public StatusBarViewModel StatusBar { get; } = new();

    public FakeOutput Output { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public FakeSystemShell SystemShell { get; } = new();

    public FakeQuickPick QuickPick { get; } = new();

    public RecordingEditorViews EditorViews { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public DockerActivity Activity { get; }

    public DockerActions Actions { get; }

    public DockerViewModel Panel { get; }

    public DockerTabs Tabs { get; }

    public DockerPanelCommands PanelCommands { get; }

    public static string Container(string id, string name, string image, string state, string status, string ports = "", string project = "", string service = "") =>
        JsonSerializer.Serialize(new { id, name, image, state, status, ports, project, service });

    public static string Image(string id, string repository, string tag, string size = "113MB", string created = "2026-07-14 08:38:19 +0700 +07") =>
        JsonSerializer.Serialize(new { id, repository, tag, size, created });

    /// <summary>A typical state: compose project "app" (api running, db stopped) and a standalone container.</summary>
    public PanelFixture WithTypicalState()
    {
        Docker.Answer("ps", string.Join('\n',
            Container("a1", "app-api-1", "app-api", "running", "Up 2 hours", "0.0.0.0:8080->80/tcp, [::]:8080->80/tcp", "app", "api"),
            Container("d1", "app-db-1", "postgres:17", "exited", "Exited (0) 3 days ago", project: "app", service: "db"),
            Container("c1", "cache", "redis:7", "running", "Up 5 minutes", "0.0.0.0:6379->6379/tcp")));
        Docker.Answer("images", string.Join('\n', Image("i1", "postgres", "17", "451MB"), Image("i2", "<none>", "<none>", "12MB")));
        return this;
    }

    public void OpenFolderWithCompose(string file = "compose.yaml", string project = "app")
    {
        Files.AddFile(Path.Combine(Root, file));
        Index.Add(Root, file);
        Workspace.Open(Root);
        Docker.Answer("compose config", $$"""
            {
              "name": "{{project}}",
              "services": { "db": {}, "api": {} }
            }
            """);
    }

    /// <summary>Shows the panel and reads state.</summary>
    public Task ShowAsync() => Panel.SetVisible(true);

    public ContainerNodeViewModel ContainerNode(string name) => Panel.Tree.AllContainers.Single(node => node.Title == name);

    public Task<CommandExecutionStatus> RunAsync(string commandId, object? argument = null) =>
        CommandService.ExecuteAsync(commandId, argument).AsTask();

    public void Dispose()
    {
        PanelCommands.Dispose();
        Panel.Dispose();
        Actions.Dispose();
        Workspace.Dispose();
    }
}
