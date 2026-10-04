using CodeEditor.Core.Modules;
using CodeEditor.Modules.Output.Resources;
using CodeEditor.Modules.Output.ViewModels;
using CodeEditor.Modules.Output.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Output.Wpf;

/// <summary>Output module views: maps <see cref="OutputViewModel"/> to <see cref="OutputView"/>.</summary>
public sealed class OutputWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("output.wpf", Strings.ViewModuleName)
    {
        Dependencies = [OutputModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<IViewRegistry>().Register<OutputViewModel, OutputView>();
}
