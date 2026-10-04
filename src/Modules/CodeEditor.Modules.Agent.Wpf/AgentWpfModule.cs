using CodeEditor.Core.Modules;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Services.Attachments;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.Wpf.Services;
using CodeEditor.Modules.Agent.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using CodeEditor.UI.Markdown;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Agent.Wpf;

/// <summary>
/// Agent chat views: maps <see cref="ChatViewModel"/> to <see cref="ChatView"/>. Code highlighting in answers comes from
/// the editor module via <see cref="ICodeColorizer"/> when it is loaded.
/// </summary>
public sealed class AgentWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("agent.wpf", Strings.AgentViewModuleName)
    {
        Dependencies = [AgentModule.Id, ShellWpfModule.Id],
    };

    // Replaces the agent module's stub picker: the last registration wins.
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IAttachmentPicker, AttachmentPicker>();

    // Resolve the colorizer when the panel is first shown, not at startup: it loads highlighting definitions.
    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<IViewRegistry>().Register<ChatViewModel>(_ => new ChatView(services.GetService<ICodeColorizer>()));
}
