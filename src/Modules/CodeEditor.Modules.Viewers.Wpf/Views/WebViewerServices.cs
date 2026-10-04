using CodeEditor.Modules.Viewers.Wpf.Services;
using CodeEditor.Shell.Theming;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>Services for WebView2 views: the shared page environment, the theme for page colors, the logger.</summary>
public sealed class WebViewerServices(ViewerWebEnvironment environment, IThemeService themes, ILogger<WebViewerServices> logger)
{
    public ViewerWebEnvironment Environment { get; } = environment;

    public IThemeService Themes { get; } = themes;

    public ILogger Logger { get; } = logger;
}
