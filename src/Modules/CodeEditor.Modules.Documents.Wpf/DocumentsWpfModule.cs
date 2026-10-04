using CodeEditor.Core.Modules;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.Modules.Documents.ViewModels;
using CodeEditor.Modules.Documents.Wpf.Services;
using CodeEditor.Modules.Documents.Wpf.Views;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Documents.Wpf;

/// <summary>
/// Document viewer views (ADR 0034): PDF on WebView2 with one environment for all tabs (<see cref="PdfEnvironment"/>),
/// Word and PowerPoint as formatted text, Excel and CSV as a grid with sheets. A view is created with its document tab;
/// WebView2 only when a PDF tab is first shown.
/// </summary>
public sealed class DocumentsWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("documents.wpf", Strings.ViewModuleName)
    {
        Dependencies = [DocumentsModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<PdfEnvironment>();

    public void Contribute(IServiceProvider services)
    {
        var views = services.GetRequiredService<IViewRegistry>();
        var shell = services.GetRequiredService<ISystemShell>();
        views.Register<PdfViewerViewModel>(_ => new PdfViewerView(services.GetRequiredService<PdfEnvironment>(), shell));
        views.Register<RichDocumentViewerViewModel>(_ => new RichDocumentView(shell));
        views.Register<SpreadsheetViewerViewModel, SpreadsheetView>();
    }
}
