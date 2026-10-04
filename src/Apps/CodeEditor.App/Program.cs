using CodeEditor.App.Diagnostics;
using CodeEditor.App.Startup;
using CodeEditor.Core.Logging;
using CodeEditor.Core.Storage;
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
    /// <param name="args">Optional folder to open: <c>Breeze.exe C:\src\project</c>.</param>
    [STAThread]
    private static int Main(string[] args)
    {
        // First of all: the installer starts the app with hook arguments (install, update, uninstall) and expects a
        // quick exit; a downloaded update is applied here too.
        VelopackApp.Build().Run();

        var clock = new StartupClock();

        // Language comes before anything that shows text: resource strings follow the thread culture.
        var paths = new UserDataPaths();
        var language = StartupLanguage.Apply(paths);

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
        LogStarting(log, SessionInfo.Version, language.Name, args.Length > 0 ? args[0] : "(last session)");

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
        session.RestoreFolder(args.Length > 0 ? args[0] : null);

        var window = host.Services.GetRequiredService<MainWindow>();
        clock.Mark("window-created");
        host.Services.GetRequiredService<StartupReporter>().Attach(window);
        window.ContentRendered += async (_, _) => await session.RestoreTabsAsync();

        var exitCode = app.Run(window);
        session.Save();

        host.StopAsync().GetAwaiter().GetResult();
        LogExited(log, exitCode);

        // Restart after saving the session, so the new process reopens the same folder and tabs.
        var restart = host.Services.GetRequiredService<AppRestart>();
        if (restart.IsRequested)
        {
            LogRestarting(log);
            (restart.Relaunch ?? Relauncher.StartNewInstance)();
        }

        return exitCode;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restarting")]
    private static partial void LogRestarting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting version {Version}, language {Language}, folder: {Folder}")]
    private static partial void LogStarting(ILogger logger, string version, string language, string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exited with code {ExitCode}")]
    private static partial void LogExited(ILogger logger, int exitCode);
}
