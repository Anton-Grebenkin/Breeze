using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Tools.Commands;
using CodeEditor.Modules.Tools.Resources;
using CodeEditor.Modules.Tools.Services;
using CodeEditor.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Tools;

/// <summary>
/// Tool shelf (ADR 0039): scripts in <c>.breeze/tools/&lt;name&gt;</c> and in the user data folder. The user runs them
/// from the "Tools" menu and the palette, the agent with <c>run_tool</c>; new or changed code asks first.
/// </summary>
public sealed class ToolsModule : IModule
{
    public const string Id = "tools";

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ToolShelf>();
        services.AddSingleton<ToolTrust>();
        services.AddSingleton<ToolRunner>();
        services.AddSingleton<ToolActivity>();
        services.AddSingleton<ToolScaffold>();
        services.AddSingleton<ToolParameterPrompt>();
        services.AddSingleton<ToolCommands>();
        services.AddSingleton<ToolAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<ToolAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<ToolAgentTools>());
        services.AddSingleton<IAgentApprovalPolicy>(provider => provider.GetRequiredService<ToolAgentTools>());
        services.AddSingleton<IAgentContextProvider, ToolAgentContext>();
    }

    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<ToolCommands>().Register(services.GetRequiredService<ICommandRegistry>(), services.GetRequiredService<IMenuRegistry>());
}
