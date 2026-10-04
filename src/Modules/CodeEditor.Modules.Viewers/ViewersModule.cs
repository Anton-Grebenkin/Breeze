using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Modules.Viewers.Commands;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Viewers;

/// <summary>
/// File viewers module (ADR 0037): images, SVG, audio, video and a hex view of binary files in editor tabs
/// (<see cref="ViewerProvider"/>). The image decoder (<see cref="IImageDecoder"/>) and WebView2 pages live in
/// <c>Viewers.Wpf</c>. Registration reads and creates nothing: a file is read when its tab is first shown.
/// </summary>
public sealed class ViewersModule : IModule
{
    public const string Id = "viewers";

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFileBytes, DiskFileBytes>();
        services.AddSingleton<ViewerContext>();
        services.AddSingleton<IFileViewerProvider, ViewerProvider>();
        services.AddSingleton<ViewerContextKeys>();
        services.AddSingleton<ViewerCommands>();
    }

    public void Contribute(IServiceProvider services)
    {
        services.GetRequiredService<ViewerCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
        services.GetRequiredService<ViewerContextKeys>().Start();
    }
}
