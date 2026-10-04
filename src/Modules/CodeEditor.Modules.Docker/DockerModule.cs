using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Docker.Commands;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services;
using CodeEditor.Modules.Docker.Services.Agent;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.ViewModels;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using CodeEditor.Modules.Docker.ViewModels.Tree;
using CodeEditor.Shell;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeEditor.Modules.Docker;

/// <summary>
/// Docker module. For the agent (ADR 0028): <c>docker</c> reads state without asking, <c>docker_change</c> builds,
/// starts and stops via an approval card; without docker installed there are no tools. For the user (ADR 0033): a side
/// bar panel with containers, images and workspace compose files plus actions; logs and details open in editor tabs.
/// Processes run only on a tool call, an action, or while the panel is visible.
/// </summary>
public sealed class DockerModule : IModule
{
    public const string Id = "docker";
    public const string ToolWindowId = "docker";

    /// <summary>After Explorer, Search and Source Control, like extension panels in VS Code.</summary>
    public const int ToolWindowOrder = 5;

    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsSection<DockerOptions>(DockerOptions.Section);
        services.AddSingleton<DockerRunner>();
        services.AddSingleton<DockerAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<DockerAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<DockerAgentTools>());
        ActionApprovalPolicy.Register<DockerOptions>(services, DockerApprovals.Rules, options => options.AlwaysAllow);
        services.AddSingleton<IAgentToolPresenter, DockerToolPresenter>();
        ConfigurePanel(services);
    }

    public void Contribute(IServiceProvider services)
    {
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.ModuleName, DockerIcons.Panel, ToolWindowLocation.SideBar, services.GetRequiredService<DockerViewModel>)
        {
            Order = ToolWindowOrder,
        }));

        var commands = services.GetRequiredService<ICommandRegistry>();
        services.GetRequiredService<DockerPanelCommands>().Register(
            commands,
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
        services.GetRequiredService<DockerTabCommands>().Register(commands);
    }

    private static void ConfigurePanel(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ComposeProjects>();
        services.AddSingleton<DockerStateReader>();
        services.AddSingleton<DockerActivity>();
        services.AddSingleton<DockerActions>();
        services.AddSingleton<DockerTabs>();
        services.AddSingleton<DockerViewModel>();
        services.AddSingleton(provider => new DockerTargets(
            provider.GetRequiredService<DockerViewModel>, provider.GetRequiredService<IQuickPick>(), provider.GetRequiredService<DockerActivity>()));
        services.AddSingleton<DockerTabCommands>();
        services.AddSingleton(provider => new DockerPanelCommands(
            provider.GetRequiredService<DockerViewModel>,
            provider.GetRequiredService<DockerTargets>(),
            provider.GetRequiredService<DockerActions>(),
            provider.GetRequiredService<DockerTabs>()));
    }
}
