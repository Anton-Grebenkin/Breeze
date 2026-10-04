using CodeEditor.Core.Modules;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.ViewModels;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using CodeEditor.Modules.Docker.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Docker.Wpf;

/// <summary>
/// Docker module views (ADR 0033): the panel (<see cref="DockerView"/>), plus container logs
/// (<see cref="ContainerLogView"/>) and inspect (<see cref="InspectView"/>) in editor tabs.
/// </summary>
public sealed class DockerWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("docker.wpf", Strings.ViewModuleName)
    {
        Dependencies = [DockerModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Contribute(IServiceProvider services)
    {
        var views = services.GetRequiredService<IViewRegistry>();
        views.Register<DockerViewModel, DockerView>();
        views.Register<ContainerLogViewModel, ContainerLogView>();
        views.Register<InspectViewModel, InspectView>();
    }
}
