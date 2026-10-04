using System.Globalization;
using CodeEditor.App.Diagnostics;
using CodeEditor.App.Instances;
using CodeEditor.App.Startup;
using CodeEditor.Core.Files;
using CodeEditor.Core.Logging;
using CodeEditor.Core.Storage;
using CodeEditor.Shell.Instances;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Session;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;

namespace CodeEditor.App;

internal static partial class Program
{
    /// <param name="args"><c>Breeze.exe [--new-window] [folder | file]</c>, see <see cref="LaunchRequest"/>.</param>
    [STAThread]
    private static int Main(string[] args)
    {
        var paths = new UserDataPaths();

        // Language comes before anything that shows text, the installer hooks' Explorer items included.
        var language = StartupLanguage.Apply(paths);
        var others = StartupRouting.OtherWindows(paths);

        // The installer starts the app with hook arguments (install, update, uninstall) and expects a quick exit. A
        // downloaded update is applied at startup only when no other window runs from the installation.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(others.Count == 0)
            .OnAfterInstallFastCallback(_ => ExplorerRegistration.Register())
            .OnAfterUpdateFastCallback(_ => ExplorerRegistration.Register())
            .OnBeforeUninstallFastCallback(_ => ExplorerRegistration.Unregister())
            .Run();

        return StartupRouting.Route(args, others) is { } plan ? Run(plan, paths, language) : 0;
    }

    private static int Run(LaunchPlan plan, UserDataPaths paths, CultureInfo language)
    {
        var clock = new StartupClock();

        // Logging right after language, so host build and module load failures are logged too.
        using var logFile = new LogFileWriter(paths.File(LogFileWriter.FolderName), SessionInfo.Describe(), TimeProvider.System);
        var levels = new LogLevelSwitch();
        using var bootstrapLogging = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddDebug()
            .AddProvider(new FileLoggerProvider(logFile, levels, TimeProvider.System)));
        var log = bootstrapLogging.CreateLogger(typeof(Program));
        var crashes = new CrashHandler(bootstrapLogging.CreateLogger<CrashHandler>(), logFile);
        crashes.AttachToProcess();
        LogStarting(log, SessionInfo.Version, language.Name, plan.Folder ?? plan.File ?? (plan.RestoreLastSession ? "(last session)" : "(new window)"));

        // Create Application first: shell services (theme) need its resources.
        var app = new App();
        app.InitializeComponent();
        clock.Mark("app-resources");

        using var host = AppHost.Build(clock, app, new AppLogging(logFile, levels, bootstrapLogging));
        clock.Mark("host-built");
        host.Start();

        var statusBar = host.Services.GetRequiredService<StatusBarViewModel>();
        crashes.AttachToDispatcher(app, message => statusBar.Message = message);

        // Folder before the window so the explorer is ready for the first frame; tabs after it so file reads don't
        // delay startup.
        var session = host.Services.GetRequiredService<SessionService>();
        session.RestoreFolder(plan.Folder, plan.RestoreLastSession);

        var window = host.Services.GetRequiredService<MainWindow>();
        clock.Mark("window-created");
        host.Services.GetRequiredService<StartupReporter>().Attach(window);
        var instance = host.Services.GetRequiredService<WindowInstance>();
        window.ContentRendered += async (_, _) =>
        {
            await session.RestoreTabsAsync();
            await instance.StartAsync(window, plan.File);
            await ExplorerRegistration.RepairAsync(bootstrapLogging.CreateLogger(typeof(ExplorerRegistration)));
        };

        var exitCode = app.Run(window);
        instance.Leave();
        session.Save();

        host.StopAsync().GetAwaiter().GetResult();
        LogExited(log, exitCode);
        Restart(host.Services, log);
        return exitCode;
    }

    // After saving the session: the new process reopens this window's folder with its tabs.
    private static void Restart(IServiceProvider services, ILogger log)
    {
        var restart = services.GetRequiredService<AppRestart>();
        if (!restart.IsRequested)
        {
            return;
        }

        LogRestarting(log);
        var folder = services.GetRequiredService<IWorkspace>().Root;
        (restart.Relaunch ?? (() => AppProcess.Start(folder is null ? [LaunchRequest.NewWindowFlag] : [folder])))();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restarting")]
    private static partial void LogRestarting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting version {Version}, language {Language}, folder: {Folder}")]
    private static partial void LogStarting(ILogger logger, string version, string language, string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exited with code {ExitCode}")]
    private static partial void LogExited(ILogger logger, int exitCode);
}
