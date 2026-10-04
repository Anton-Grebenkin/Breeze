using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Diagrams.Commands;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Agent;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Diagrams;

/// <summary>
/// Diagrams module (ADR 0035): Mermaid diagrams in <c>.mmd</c> and <c>.mermaid</c> files and Markdown
/// <c>```mermaid</c> blocks. Files open in the regular text editor; the module adds a live preview in a side tab, SVG
/// and PNG export and the <c>diagram</c> agent tool. The renderer (<see cref="IDiagramRenderer"/>) lives in
/// <c>Diagrams.Wpf</c> and is created on first render, so it does not affect startup.
/// </summary>
public sealed class DiagramsModule : IModule
{
    public const string Id = "diagrams";

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DiagramWrites>();
        services.AddSingleton<DiagramTextReader>();
        services.AddSingleton<DiagramExporter>();
        services.AddSingleton<DiagramExports>();
        services.AddSingleton<DiagramPreviewServices>();
        services.AddSingleton<DiagramPreviews>();
        services.AddSingleton<DiagramEditorContext>();
        services.AddSingleton<DiagramCommands>();

        services.AddSingleton<DiagramToolInputs>();
        services.AddSingleton<DiagramRenderTargets>();
        services.AddSingleton<DiagramAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<DiagramAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<DiagramAgentTools>());
        services.AddSingleton<IAgentApprovalPolicy, DiagramApprovals>();
        services.AddSingleton<IAgentToolPresenter, DiagramToolPresenter>();
    }

    // Without a renderer (windowless host) there is no preview or export: no commands, and the agent gets no tool.
    public void Contribute(IServiceProvider services)
    {
        if (services.GetService<IDiagramRenderer>() is null)
        {
            return;
        }

        services.GetRequiredService<DiagramCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
        services.GetRequiredService<DiagramEditorContext>().Start();
    }
}
