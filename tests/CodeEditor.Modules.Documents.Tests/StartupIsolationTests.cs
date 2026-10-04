using System.Reflection;
using System.Runtime.Loader;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// The module doesn't touch format libraries at startup: service registration, the viewer provider, tab creation and
/// agent tools work without Open XML SDK, PdfPig, PDFsharp, MigraDoc and Markdig. The module loads into a separate
/// assembly context, which shows what got pulled in even if other tests in the process already loaded those libraries.
/// </summary>
public sealed class StartupIsolationTests
{
    private const string ModuleAssembly = "CodeEditor.Modules.Documents";
    private static readonly string[] Libraries = ["DocumentFormat.OpenXml", "UglyToad.PdfPig", "PdfSharp", "MigraDoc", "Markdig"];

    [Fact]
    public void Startup_DoesNotLoadFormatLibraries()
    {
        var context = new IsolatedContext(AppContext.BaseDirectory);
        try
        {
            var assembly = context.LoadFromAssemblyName(new AssemblyName(ModuleAssembly));
            var module = (IModule)Activator.CreateInstance(assembly.GetType(ModuleAssembly + ".DocumentsModule", throwOnError: true)!)!;
            using var services = Services(module);

            var viewers = services.GetRequiredService<IFileViewerProvider>();
            Assert.True(viewers.CanOpen("отчёт.pdf"));
            (viewers.CreateViewer(Path.Combine(DocumentsFixture.Root, "отчёт.docx")) as IDisposable)?.Dispose();
            var tools = services.GetServices<IAgentToolProvider>().SelectMany(provider => provider.CreateTools()).Select(tool => tool.Name).ToList();
            _ = services.GetRequiredService<IAgentToolPresenter>();

            Assert.Equal(["document", "document_change"], tools);
            var loaded = context.Assemblies.Select(item => item.GetName().Name!).ToList();
            Assert.DoesNotContain(loaded, name => Libraries.Any(library => name.StartsWith(library, StringComparison.Ordinal)));
            Assert.Contains(ModuleAssembly, loaded);
        }
        finally
        {
            context.Unload();
        }
    }

    private static ServiceProvider Services(IModule module)
    {
        var files = new FakeFileSystem().AddDirectory(DocumentsFixture.Root);
        var workspace = new Workspace(files, new Core.Context.ContextKeyService(), NullLogger<Workspace>.Instance);
        workspace.Open(DocumentsFixture.Root);
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IFileSystem>(files);
        services.AddSingleton<IWorkspace>(workspace);
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        services.AddSingleton<ISystemShell, FakeSystemShell>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICommandService, DocumentsFixture.RecordingCommands>();
        services.AddSingleton<IAgentOutputStore, PassThrough>();
        module.ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    private sealed class PassThrough : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }

    // The module and format libraries load here; everything else (core, shell, contracts) is shared with the test.
    private sealed class IsolatedContext(string folder) : AssemblyLoadContext("documents-startup", isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var name = assemblyName.Name ?? string.Empty;
            if (name != ModuleAssembly && !Libraries.Any(library => name.StartsWith(library, StringComparison.Ordinal)))
            {
                return null;
            }

            var path = Path.Combine(folder, name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }
}
