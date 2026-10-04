using CodeEditor.Core.Modules;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Modules.Viewers.Wpf.Services;
using CodeEditor.Modules.Viewers.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Viewers.Wpf;

/// <summary>
/// File viewer views (ADR 0037): images use WPF and Windows codecs (<see cref="WicImageDecoder"/>), the hex view a
/// virtualized list, SVG and media WebView2 pages in a shared environment (<see cref="ViewerWebEnvironment"/>). A view is
/// created with its tab; WebView2 when an SVG or media tab is first shown.
/// </summary>
public sealed class ViewersWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("viewers.wpf", Strings.ViewModuleName)
    {
        Dependencies = [ViewersModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IImageDecoder, WicImageDecoder>();
        services.AddSingleton<ViewerWebEnvironment>();
        services.AddSingleton<WebViewerServices>();
    }

    // Page services are resolved when a view is created, so registration does not touch WebView2.
    public void Contribute(IServiceProvider services)
    {
        var views = services.GetRequiredService<IViewRegistry>();
        views.Register<ImageViewerViewModel, ImageViewerView>();
        views.Register<HexViewerViewModel, HexViewerView>();
        views.Register<SvgViewerViewModel>(_ => new SvgViewerView(services.GetRequiredService<WebViewerServices>()));
        views.Register<MediaViewerViewModel>(_ => new MediaViewerView(services.GetRequiredService<WebViewerServices>()));
    }
}
