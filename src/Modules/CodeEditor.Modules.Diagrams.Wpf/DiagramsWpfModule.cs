using CodeEditor.Core.Modules;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Modules.Diagrams.Wpf.Services;
using CodeEditor.Modules.Diagrams.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Diagrams.Wpf;

/// <summary>
/// Views of the Diagrams module (ADR 0035): the Mermaid renderer on a hidden WebView2 page
/// (<see cref="WebViewDiagramRenderer"/>) and the preview tab. WebView2 assemblies load on the first diagram render.
/// </summary>
public sealed class DiagramsWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("diagrams.wpf", Strings.ViewModuleName)
    {
        Dependencies = [DiagramsModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DiagramWebEnvironment>();
        services.AddSingleton<WebViewDiagramRenderer>();
        services.AddSingleton<IDiagramRenderer>(provider => provider.GetRequiredService<WebViewDiagramRenderer>());
    }

    public void Contribute(IServiceProvider services)
    {
        var environment = services.GetRequiredService<DiagramWebEnvironment>();
        var logger = services.GetRequiredService<ILogger<DiagramPreviewView>>();
        services.GetRequiredService<IViewRegistry>().Register<DiagramPreviewViewModel>(_ => new DiagramPreviewView(environment, logger));
    }
}
