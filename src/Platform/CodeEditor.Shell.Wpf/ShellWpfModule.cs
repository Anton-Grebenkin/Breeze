using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Threading;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Theming;
using CodeEditor.Shell.Wpf.Services;
using CodeEditor.Shell.Wpf.Commands;
using CodeEditor.Shell.Wpf.Diagnostics;
using CodeEditor.Shell.Wpf.Input;
using CodeEditor.Shell.Wpf.Presentation;
using CodeEditor.Shell.Wpf.Theming;
using CodeEditor.Shell.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Wpf;

/// <summary>
/// Shell views: main window, theme, keyboard, view registry, window commands. Applies the theme before the window is
/// created.
/// </summary>
public sealed class ShellWpfModule : IModule
{
    public const string Id = "shell.wpf";

    public ModuleInfo Info { get; } = new(Id, Strings.ShellWpfModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IViewRegistry, ViewRegistry>();
        services.AddSingleton<IFileDialogs, FileDialogs>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<ISystemShell, SystemShell>();
        services.AddSingleton<KeyboardRouter>();
        services.AddSingleton<WindowCommands>();
        services.AddSingleton<MainWindow>();
    }

    public void Contribute(IServiceProvider services)
    {
        BindingErrorListener.Install(services.GetRequiredService<ILogger<BindingErrorListener>>());

        services.GetRequiredService<ThemeSettings>().Start();

        services.GetRequiredService<WindowCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
    }
}
