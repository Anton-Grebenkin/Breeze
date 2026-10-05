using System.Reflection;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Instances;
using CodeEditor.Shell.Integration;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Session;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Settings;
using CodeEditor.Shell.Theming;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Workspace;
using CodeEditor.Shell.Zoom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeEditor.Shell;

/// <summary>
/// The shell as a module: window ViewModels, palette, menus, layout and shell commands. Views live in
/// <c>ShellWpfModule</c>.
/// </summary>
public sealed class ShellModule : IModule
{
    public const string Id = "shell";

    public ModuleInfo Info { get; } = new(Id, Strings.ShellModuleName);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MenuBuilder>();
        services.AddSingleton<MenuViewModelFactory>();
        services.AddSingleton<RecentCommands>();
        services.AddSingleton<IQuickOpenProvider, CommandsQuickOpenProvider>();
        services.AddSingleton<IQuickOpenProvider, FilesQuickOpenProvider>();
        services.AddSingleton<IQuickOpenProvider, GoToLineQuickOpenProvider>();
        services.AddSingleton<CommandPaletteViewModel>();
        services.AddSingleton<IQuickPick>(provider => provider.GetRequiredService<CommandPaletteViewModel>());

        services.AddSingleton<IToolWindowRegistry, ToolWindowRegistry>();
        services.AddSingleton<ILayoutStore, JsonLayoutStore>();
        services.AddSingleton<ToolWindowPlacement>();
        services.AddSingleton<EditorToolWindows>();
        services.AddSingleton<WorkbenchLayout>();

        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<NotificationsViewModel>();
        services.AddSingleton<INotificationService>(provider => provider.GetRequiredService<NotificationsViewModel>());
        services.AddSingleton<TitleBarViewModel>();
        services.AddSingleton<ActivityBarViewModel>();
        services.AddSingleton<WelcomeViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<WorkbenchContributions>();
        services.AddSingleton<ThemeCommands>();
        services.AddSingleton<LanguageCommands>();
        services.AddSingleton<AppRestart>();
        services.AddSingleton<SettingsCommands>();
        services.AddSingleton<DeveloperCommands>();
        services.AddSingleton<LogCommands>();
        services.AddSingleton<HelpCommands>();
        services.TryAddSingleton(_ => ProductInfo.FromAssembly(Assembly.GetEntryAssembly() ?? typeof(ShellModule).Assembly, MainWindowViewModel.ProductName));
        services.AddSingleton<ThemeSettings>();
        services.AddSettingsSection<WorkbenchOptions>(WorkbenchOptions.Section);
        services.AddSingleton<LayoutCommands>();
        services.AddSettingsSection<WindowOptions>(WindowOptions.Section);
        services.AddSingleton<WindowZoom>();
        services.AddSingleton<ZoomCommands>();

        services.AddSingleton<RecentFolders>();
        services.AddSingleton<IProcessProbe, SystemProcessProbe>();
        services.AddSingleton<WindowRegistry>();
        services.AddSingleton<InstanceServer>();

        // The app replaces it with the installed build's integration.
        services.AddSingleton<IWindowsIntegration, NoWindowsIntegration>();
        services.AddSettingsSection<WindowsIntegrationOptions>(WindowsIntegrationOptions.Section);
        services.AddSingleton<FileTypesPrompt>();
        services.AddSingleton<WindowsIntegrationCommands>();
        services.AddSingleton<IAppWindows, AppWindows>();
        services.AddSingleton<FolderTabs>();
        services.AddSingleton<WorkspaceSwitcher>();
        services.AddSingleton<WorkspaceCommands>();

        services.AddSingleton<DocumentSaver>();
        services.AddSingleton<EditorAreaViewModel>();
        services.AddSingleton<EditorTabRelocator>();
        services.AddSingleton<IEditorViews, EditorViews>();
        services.AddSingleton<IShutdownGuard>(provider => provider.GetRequiredService<EditorAreaViewModel>());
        services.AddSingleton<EditorCommands>();
        services.AddSingleton<EditorGroupCommands>();
        services.AddSingleton<EditorAreaHost>();
        services.AddSingleton<RecentFiles>();
        services.TryAddSingleton(TimeProvider.System);
        // The Browser module replaces it so pages open in a tab next to files.
        services.TryAddSingleton<IWebPageOpener, SystemWebPageOpener>();
        services.AddSingleton<AutoSaveService>();
        services.AddSingleton<ISessionStore, JsonSessionStore>();
        services.AddSingleton<SessionService>();
    }

    public void Contribute(IServiceProvider services)
    {
        var commands = services.GetRequiredService<ICommandRegistry>();
        var keybindings = services.GetRequiredService<IKeybindingRegistry>();
        var menus = services.GetRequiredService<IMenuRegistry>();

        services.GetRequiredService<WorkbenchContributions>().Register(commands, keybindings, menus);
        services.GetRequiredService<ThemeCommands>().Register(commands, keybindings, menus);
        services.GetRequiredService<LanguageCommands>().Register(commands, menus);
        services.GetRequiredService<SettingsCommands>().Register(commands, keybindings, menus);
        services.GetRequiredService<DeveloperCommands>().Register(commands);
        services.GetRequiredService<LogCommands>().Register(commands);
        services.GetRequiredService<HelpCommands>().Register(commands, menus);
        services.GetRequiredService<LayoutCommands>().Register(
            commands, keybindings, menus, services.GetRequiredService<IToolWindowRegistry>());
        services.GetRequiredService<ZoomCommands>().Register(commands, keybindings, menus);
        services.GetRequiredService<WorkspaceCommands>().Register(commands, keybindings, menus);
        services.GetRequiredService<WindowsIntegrationCommands>().Register(commands, menus);
        services.GetRequiredService<EditorCommands>().Register(commands, keybindings, menus);
        services.GetRequiredService<EditorGroupCommands>().Register(commands, keybindings, menus);

        // Creating it subscribes the recent files history to active tab changes.
        _ = services.GetRequiredService<RecentFiles>();

        // files.exclude must apply before the command-line folder opens.
        services.GetRequiredService<ExcludeSettings>().Start();
        _ = services.GetRequiredService<AutoSaveService>();
    }
}
