using CodeEditor.Core.Modules;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Modules.Browser.Wpf.Services;
using CodeEditor.Modules.Browser.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Browser.Wpf;

/// <summary>
/// Browser module view (ADR 0027): the WebView2 browser (<see cref="WebViewBrowserEngine"/>) and its panel. WebView2
/// assemblies load on first panel show.
/// </summary>
public sealed class BrowserWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("browser.wpf", Strings.ViewModuleName)
    {
        Dependencies = [BrowserModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<WebViewBrowserEngine>();
        services.AddSingleton<IBrowserEngine>(provider => provider.GetRequiredService<WebViewBrowserEngine>());
    }

    // The browser is created with the first panel or agent call, so startup does not load WebView2.
    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<IViewRegistry>().Register<BrowserViewModel>(_ => new BrowserView(services.GetRequiredService<WebViewBrowserEngine>()));
}
