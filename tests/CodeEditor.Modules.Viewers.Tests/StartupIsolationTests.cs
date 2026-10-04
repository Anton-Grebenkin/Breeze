using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Theming;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Wpf.Presentation;
using CodeEditor.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// The module doesn't affect startup: registering both modules (services, commands, views), the viewer provider and
/// creating tabs of every kind need neither WebView2 assemblies nor file reads. The modules load into a separate
/// assembly load context, which shows what got loaded even if other tests in the process already loaded WebView2.
/// </summary>
public sealed class StartupIsolationTests
{
    private const string LogicAssembly = "CodeEditor.Modules.Viewers";
    private const string ViewsAssembly = "CodeEditor.Modules.Viewers.Wpf";
    private const string WebView2 = "Microsoft.Web.WebView2";

    private static readonly string[] Isolated = [LogicAssembly, ViewsAssembly, WebView2];

    [Fact]
    public void Registration_AndNewTabs_DoNotLoadWebView2()
    {
        var context = new IsolatedContext(AppContext.BaseDirectory);
        var logic = Module(context, LogicAssembly, "CodeEditor.Modules.Viewers.ViewersModule");
        var views = Module(context, ViewsAssembly, "CodeEditor.Modules.Viewers.Wpf.ViewersWpfModule");
        var registered = new RecordingViews();
        using var services = Services(registered, logic, views);

        logic.Contribute(services);
        views.Contribute(services);
        var viewers = services.GetRequiredService<IFileViewerProvider>();
        foreach (var name in new[] { "pic.png", "logo.svg", "song.mp3", "clip.mp4", "app.exe" })
        {
            Assert.True(viewers.CanOpen(name));
            (viewers.CreateViewer(Path.Combine(ViewersFixture.Root, name)) as IDisposable)?.Dispose();
        }

        _ = services.GetRequiredService(context.Assemblies.Single(item => item.GetName().Name == ViewsAssembly).GetType("CodeEditor.Modules.Viewers.Wpf.Views.WebViewerServices", throwOnError: true)!);
        var loaded = context.Assemblies.Select(item => item.GetName().Name!).ToList();
        Assert.DoesNotContain(loaded, name => name.StartsWith(WebView2, StringComparison.Ordinal));
        Assert.Contains(ViewsAssembly, loaded);
        Assert.Equal(["ImageViewerViewModel", "HexViewerViewModel", "SvgViewerViewModel", "MediaViewerViewModel"], registered.ViewModels);
    }

    private static IModule Module(IsolatedContext context, string assembly, string type) =>
        (IModule)Activator.CreateInstance(context.LoadFromAssemblyName(new AssemblyName(assembly)).GetType(type, throwOnError: true)!)!;

    private static ServiceProvider Services(RecordingViews views, params IModule[] modules)
    {
        var files = new FakeFileSystem().AddDirectory(ViewersFixture.Root);
        var workspace = new Workspace(files, new ContextKeyService(), NullLogger<Workspace>.Instance);
        workspace.Open(ViewersFixture.Root);
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IFileSystem>(files);
        services.AddSingleton<IWorkspace>(workspace);
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        services.AddSingleton<ISystemShell, FakeSystemShell>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new UserDataPaths(Path.Combine(Path.GetTempPath(), "codeeditor-viewers-startup")));
        services.AddSingleton<ICommandService, RecordingCommands>();
        services.AddSingleton<IContextKeyService, ContextKeyService>();
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        services.AddSingleton<IKeybindingRegistry, KeybindingRegistry>();
        services.AddSingleton<IMenuRegistry, MenuRegistry>();
        services.AddSingleton<IQuickPick, FakeQuickPick>();
        services.AddSingleton<IThemeService, DarkTheme>();
        services.AddSingleton<IViewRegistry>(views);
        services.AddSingleton<IDocumentService>(_ => new DocumentService(files, new TestTextBufferFactory(), new InlineUiDispatcher(), workspace, NullLogger<DocumentService>.Instance));
        services.AddSingleton<IDialogService, FakeDialogs>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<DocumentSaver>();
        services.AddSingleton<EditorAreaViewModel>();
        foreach (var module in modules)
        {
            module.ConfigureServices(services);
        }

        return services.BuildServiceProvider();
    }

    // Modules and WebView2 go into this context; everything else (core, shell, WPF) is shared with the test.
    private sealed class IsolatedContext(string folder) : AssemblyLoadContext("viewers-startup")
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var name = assemblyName.Name ?? string.Empty;
            if (!Isolated.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) || name.EndsWith(".resources", StringComparison.Ordinal))
            {
                return null;
            }

            var path = Path.Combine(folder, name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }

    private sealed class RecordingViews : IViewRegistry
    {
        public List<string> ViewModels { get; } = [];

        public void Register<TViewModel, TView>()
            where TView : FrameworkElement, new() => ViewModels.Add(typeof(TViewModel).Name);

        public void Register<TViewModel>(Func<TViewModel, FrameworkElement> factory)
            where TViewModel : class => ViewModels.Add(typeof(TViewModel).Name);
    }

    private sealed class DarkTheme : IThemeService
    {
        public ThemeKind Current => ThemeKind.Dark;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public void Apply(ThemeKind theme)
        {
        }
    }
}
