using System.Windows;
using CodeEditor.App.Diagnostics;
using CodeEditor.App.Instances;
using CodeEditor.App.Startup;
using CodeEditor.Core;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Logging;
using CodeEditor.Core.Modules;
using CodeEditor.Modules.Agent;
using CodeEditor.Modules.Agent.Wpf;
using CodeEditor.Modules.Browser;
using CodeEditor.Modules.Browser.Wpf;
using CodeEditor.Modules.Diagrams;
using CodeEditor.Modules.Diagrams.Wpf;
using CodeEditor.Modules.Docker;
using CodeEditor.Modules.Docker.Wpf;
using CodeEditor.Modules.Tools;
using CodeEditor.Modules.Documents;
using CodeEditor.Modules.Documents.Wpf;
using CodeEditor.Modules.Explorer;
using CodeEditor.Modules.Explorer.Wpf;
using CodeEditor.Modules.Git;
using CodeEditor.Modules.Git.Wpf;
using CodeEditor.Modules.Search;
using CodeEditor.Modules.Search.Wpf;
using CodeEditor.Modules.Output;
using CodeEditor.Modules.Output.Wpf;
using CodeEditor.Modules.TextEditor;
using CodeEditor.Modules.TextEditor.Wpf;
using CodeEditor.Modules.Terminal;
using CodeEditor.Modules.Terminal.Wpf;
using CodeEditor.Modules.Updates;
using CodeEditor.Modules.Viewers;
using CodeEditor.Modules.Viewers.Wpf;
using CodeEditor.Shell;
using CodeEditor.Shell.Integration;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeEditor.App;

/// <summary>Builds the app's Generic Host: core, modules and startup services.</summary>
internal static class AppHost
{
    public static IHost Build(StartupClock clock, Application application, AppLogging logging)
    {
        // Empty builder: no appsettings, environment variables or EventLog, for faster startup.
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = MainWindowViewModel.ProductName,
        });

        // Providers filter levels themselves: the file by the log.level setting, the Output panel from Information.
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new FileLoggerProvider(logging.File, logging.Levels, TimeProvider.System));

        // A windowed app doesn't need the host's console messages ("Press Ctrl+C to shut down").
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);

        var services = builder.Services;
        services.AddSingleton(application);
        services.AddSingleton(clock);
        services.AddSingleton(logging.File);
        services.AddSingleton<ILogFiles>(logging.File);
        services.AddSingleton(logging.Levels);
        services.AddSingleton<StartupReporter>();
        services.AddSingleton<WindowInstance>();
        services.AddSingleton(ProductInfo.FromAssembly(typeof(AppHost).Assembly, MainWindowViewModel.ProductName));
        services.AddCodeEditorCore();

        // The module loader needs a logger before the container exists.
        var loader = new ModuleLoader(
            ModuleCatalog.Create(Modules()),
            logging.Bootstrap.CreateLogger<ModuleLoader>());
        loader.ConfigureServices(services);

        // After the modules, so it replaces the shell's default: a module's TryAdd doesn't see the app's services.
        services.AddSingleton<ExplorerRegistration>();
        services.AddSingleton<IWindowsIntegration>(provider => provider.GetRequiredService<ExplorerRegistration>());

        clock.Mark("modules-configured");
        var host = builder.Build();
        clock.Mark("container-built");
        host.Services.GetRequiredService<LogLevelSettings>().Start();
        loader.Contribute(host.Services);

        // User keybindings go after modules: later registrations take precedence.
        host.Services.GetRequiredService<UserKeybindings>().Start();
        clock.Mark("modules-contributed");
        return host;
    }

    /// <summary>App modules, referenced as projects for now (dynamic loading is planned for M4).</summary>
    private static IModule[] Modules() =>
    [
        new ShellModule(),
        new ShellWpfModule(),
        new OutputModule(),
        new OutputWpfModule(),
        new ExplorerModule(),
        new ExplorerWpfModule(),
        new SearchModule(),
        new SearchWpfModule(),
        new AgentModule(),
        new AgentWpfModule(),
        new TextEditorModule(),
        new TextEditorWpfModule(),
        new TerminalModule(),
        new TerminalWpfModule(),
        new GitModule(),
        new GitWpfModule(),
        new DockerModule(),
        new DockerWpfModule(),
        new BrowserModule(),
        new BrowserWpfModule(),
        new DiagramsModule(),
        new DiagramsWpfModule(),
        new DocumentsModule(),
        new DocumentsWpfModule(),
        new ViewersModule(),
        new ViewersWpfModule(),
        new ToolsModule(),
        new UpdatesModule(),
    ];
}
