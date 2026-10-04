using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Explorer.Commands;
using CodeEditor.Modules.Explorer.Resources;
using CodeEditor.Modules.Explorer.Services;
using CodeEditor.Modules.Explorer.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Explorer;

/// <summary>
/// Explorer module: file tree in the side bar (<c>Ctrl+Shift+E</c>), file operations and the context menu.
/// Opening a folder shows the explorer automatically, as in VS Code.
/// </summary>
public sealed class ExplorerModule : IModule
{
    public const string Id = "explorer";
    public const string ToolWindowId = "explorer";

    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AgentChangeMarks>();
        services.AddSingleton<ExplorerViewModel>();
        services.AddSingleton<ExplorerEditor>();
        services.AddSingleton<ExplorerDrop>();
        services.AddSingleton<ExplorerPanelViewModel>();
        services.AddSingleton<ExplorerCommands>();
        services.AddSingleton<IAgentToolProvider, ExplorerAgentTools>();
        services.AddSingleton<ExplorerEditingTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<ExplorerEditingTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<ExplorerEditingTools>());
        services.AddSingleton<IAgentToolPresenter, ExplorerToolPresenter>();
    }

    public void Contribute(IServiceProvider services)
    {
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.ModuleName, IconNames.Explorer, ToolWindowLocation.SideBar,
            services.GetRequiredService<ExplorerPanelViewModel>)
        {
            Order = 0,
            Keybinding = "Ctrl+Shift+E",
        }));

        services.GetRequiredService<ExplorerCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());

        var workspace = services.GetRequiredService<IWorkspace>();
        var layout = services.GetRequiredService<WorkbenchLayout>();
        workspace.Changed += (_, _) =>
        {
            if (workspace.Root is not null)
            {
                layout.Show(ToolWindowId, focus: false);
            }
        };

        // Reveal the active file in the tree, as in VS Code.
        var editors = services.GetRequiredService<EditorAreaViewModel>();
        var explorer = services.GetRequiredService<ExplorerViewModel>();
        editors.ActiveDocumentChanged += (_, _) => _ = explorer.RevealAsync(editors.Active?.FilePath);
    }
}
