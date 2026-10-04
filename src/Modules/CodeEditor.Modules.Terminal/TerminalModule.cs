using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Terminal.Commands;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Build;
using CodeEditor.Modules.Terminal.Services.Commands;
using CodeEditor.Modules.Terminal.Services.Pty;
using CodeEditor.Modules.Terminal.Services.Shells;
using CodeEditor.Modules.Terminal.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeEditor.Modules.Terminal;

/// <summary>
/// Terminal module: the Terminal panel (shells in a Windows pseudo console, ADR 0045), and process launching and .NET
/// build and tests as user commands and agent tools (<c>build</c>, <c>run_tests</c>, <c>get_errors</c>,
/// <c>run_command</c>). Processes start only on an explicit command; the panel's first shell starts when it is shown.
/// </summary>
public sealed class TerminalModule : IModule
{
    public const string Id = "terminal";
    public const string ToolWindowId = "terminal";

    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsSection<TerminalOptions>(TerminalOptions.Section);
        services.AddSingleton<IAgentApprovalPolicy, CommandApprovals>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SaveBeforeRun>();
        services.AddSingleton<DotNetTarget>();
        services.AddSingleton<DotNetBuild>();
        services.AddSingleton<DotNetTests>();
        services.AddSingleton<IAgentToolProvider, BuildAgentTools>();
        services.AddSingleton<IAgentVerifier, DotNetVerifier>();
        services.AddSingleton<BackgroundCommands>();
        services.AddSingleton<IAgentToolProvider, BackgroundCommandTools>();
        services.AddSingleton<IAgentContextProvider, BackgroundCommandsContext>();
        services.AddSingleton<CommandAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<CommandAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<CommandAgentTools>());
        services.AddSingleton<IAgentContextProvider, BuildAgentContext>();
        services.AddSingleton<IAgentToolPresenter, TerminalToolPresenter>();
        services.AddSingleton<TerminalCommands>();
        services.AddSingleton(ShellLocations.FromSystem());
        services.AddSingleton<TerminalProfiles>();
        services.AddSingleton<ITerminalProcessFactory, ConPtyProcessFactory>();
        services.AddSingleton<TerminalPanelViewModel>();
        services.AddSingleton<TerminalPanelCommands>();
    }

    public void Contribute(IServiceProvider services)
    {
        var commands = services.GetRequiredService<ICommandRegistry>();
        var keybindings = services.GetRequiredService<IKeybindingRegistry>();
        services.GetRequiredService<TerminalCommands>().Register(commands, keybindings);
        services.GetRequiredService<TerminalPanelCommands>().Register(commands, keybindings, services.GetRequiredService<IMenuRegistry>());
        _registrations.Add(services.GetRequiredService<KeyCaptures>().Register(new KeyCapture(TerminalPanelViewModel.FocusContextKey, TerminalKeys.KeepsKeys)));
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.ModuleName, IconNames.Terminal, ToolWindowLocation.Panel, services.GetRequiredService<TerminalPanelViewModel>)
        {
            Order = 1,
            Keybinding = "Ctrl+`",
        }));
    }
}
