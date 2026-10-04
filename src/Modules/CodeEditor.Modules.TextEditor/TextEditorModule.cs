using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Commands;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Shell;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.TextEditor;

/// <summary>
/// Text editor module: the editor provider for any text file, zoom, undo and redo.
/// AvalonEdit buffers and views live in <c>TextEditor.Wpf</c>.
/// </summary>
public sealed class TextEditorModule : IModule
{
    public const string Id = "texteditor";

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<EditorSettings>();
        services.AddSettingsSection<EditorOptions>(EditorOptions.Section);
        services.AddSingleton<EditorFontZoom>();
        services.AddSingleton<EditorFocus>();
        services.AddSingleton<IEditorProvider, TextEditorProvider>();
        services.AddSingleton<TextEditorCommands>();
        services.AddSingleton<AgentChangeCommands>();
        services.AddSingleton<EditorStatusItems>();
        services.AddSingleton<IAgentToolProvider, EditorAgentTools>();
        services.AddSingleton<IAgentContextProvider, EditorAgentContext>();
        services.AddSingleton<IAgentChangeReverter, AgentChangeReverter>();
        // The reverter depends on the editor area, which depends on editor providers: Lazy breaks the cycle.
        services.AddSingleton(provider => new Lazy<IAgentChangeReverter>(provider.GetRequiredService<IAgentChangeReverter>));
        services.AddSingleton<EditPlanner>();
        services.AddSingleton<EditingAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<EditingAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<EditingAgentTools>());
        services.AddSingleton<IAgentToolPresenter, EditorToolPresenter>();
    }

    public void Contribute(IServiceProvider services)
    {
        services.GetRequiredService<TextEditorCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
        services.GetRequiredService<AgentChangeCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());

        // Resolving it binds the status bar items to the active editor.
        _ = services.GetRequiredService<EditorStatusItems>();
    }
}
