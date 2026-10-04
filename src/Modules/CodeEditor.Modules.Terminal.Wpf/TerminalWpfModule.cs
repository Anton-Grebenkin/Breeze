using CodeEditor.Core.Modules;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.ViewModels;
using CodeEditor.Modules.Terminal.Wpf.Services;
using CodeEditor.Modules.Terminal.Wpf.Views;
using CodeEditor.Shell.Theming;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Terminal.Wpf;

/// <summary>
/// The Terminal panel view (ADR 0045): xterm.js on a WebView2 page. WebView2 assemblies load when the panel is first
/// shown.
/// </summary>
public sealed class TerminalWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("terminal.wpf", Strings.ViewModuleName)
    {
        Dependencies = [TerminalModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<TerminalWebEnvironment>();

    public void Contribute(IServiceProvider services)
    {
        var environment = services.GetRequiredService<TerminalWebEnvironment>();
        var themes = services.GetRequiredService<IThemeService>();
        var logger = services.GetRequiredService<ILogger<TerminalView>>();
        services.GetRequiredService<IViewRegistry>().Register<TerminalPanelViewModel>(_ => new TerminalView(environment, themes, logger));
    }
}
