using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
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
using CodeEditor.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeEditor.Modules.Terminal;

/// <summary>
/// Terminal module: process launching and .NET build and tests, as user commands and agent tools (<c>build</c>,
/// <c>run_tests</c>, <c>get_errors</c>, <c>run_command</c>). Processes start only on an explicit command.
/// </summary>
public sealed class TerminalModule : IModule
{
    public const string Id = "terminal";

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
    }

    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<TerminalCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>());
}
