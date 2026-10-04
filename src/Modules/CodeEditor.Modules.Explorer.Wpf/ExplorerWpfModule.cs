using CodeEditor.Core.Modules;
using CodeEditor.Modules.Explorer.Resources;
using CodeEditor.Modules.Explorer.ViewModels;
using CodeEditor.Modules.Explorer.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Explorer.Wpf;

/// <summary>Explorer module views: maps <see cref="ExplorerPanelViewModel"/> to <see cref="ExplorerView"/>.</summary>
public sealed class ExplorerWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("explorer.wpf", Strings.ViewModuleName)
    {
        Dependencies = [ExplorerModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<IViewRegistry>().Register<ExplorerPanelViewModel, ExplorerView>();
}
