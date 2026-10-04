using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Git.Commands;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Agent;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Modules.Git.ViewModels.Tabs;
using CodeEditor.Shell;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeEditor.Modules.Git;

/// <summary>
/// Git module. Agent tools (ADR 0024): <c>git</c> reads without asking, <c>git_change</c> changes via an approval card.
/// Source Control panel (ADR 0032, <c>Ctrl+Shift+G</c>): branch, commit, index, discard, diffs and history in editor
/// tabs, branch in the status bar. State is read in the background on folder open, panel show and changes to files
/// or .git; the panel is created on first show.
/// </summary>
public sealed class GitModule : IModule
{
    public const string Id = "git";
    public const string ToolWindowId = "git";

    /// <summary>
    /// Panel icon. In Codicons "source-control" is an alias of the same glyph U+EA68 as "git-branch"; the icon table
    /// (codicon.csv) lists only the primary name.
    /// </summary>
    public const string Icon = "git-branch";

    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsSection<GitOptions>(GitOptions.Section);
        services.AddSingleton<GitRunner>();
        services.AddSingleton<GitAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<GitAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<GitAgentTools>());
        ActionApprovalPolicy.Register<GitOptions>(services, GitApprovals.Rules, options => options.AlwaysAllow);
        services.AddSingleton<IAgentToolPresenter, GitToolPresenter>();
        ConfigurePanel(services);
    }

    public void Contribute(IServiceProvider services)
    {
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.PanelTitle, Icon, ToolWindowLocation.SideBar, services.GetRequiredService<GitPanelViewModel>)
        {
            Order = 2,
            Keybinding = "Ctrl+Shift+G",
        }));

        services.GetRequiredService<GitPanelCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>());
        services.GetRequiredService<GitMenus>().Register(services.GetRequiredService<IMenuRegistry>());

        // Creating these subscribes refresh to workspace events and puts the branch into the status bar.
        _ = services.GetRequiredService<GitAutoRefresh>();
        _ = services.GetRequiredService<GitStatusBarItem>();
    }

    private static void ConfigurePanel(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<GitCli>();
        services.AddSingleton<GitReader>();
        services.AddSingleton<GitRepository>();
        services.AddSingleton<GitActions>();
        services.AddSingleton<GitAutoRefresh>();
        services.AddSingleton<GitStatusBarItem>();
        services.AddSingleton<GitChangesViewModel>();
        services.AddSingleton<GitCommitInputViewModel>();
        services.AddSingleton<GitDiffFactory>();
        services.AddSingleton<GitEditorTabs>();
        services.AddSingleton<GitBranchPicker>();
        services.AddSingleton<GitChangeHandlers>();
        services.AddSingleton<GitPanelViewModel>();
        services.AddSingleton<GitPanelCommands>();
        services.AddSingleton<GitMenus>();
    }
}
