using CodeEditor.Core.Commands;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Output;
using CodeEditor.Modules.Output.Resources;
using CodeEditor.Modules.Output.Services;
using CodeEditor.Modules.Output.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Output;

/// <summary>
/// Output module: output channels, the application log and the bottom panel. The panel, its "Show" command
/// and the View menu item come from contributions; the shell is not changed.
/// </summary>
public sealed class OutputModule : IModule
{
    public const string Id = "output";
    public const string ToolWindowId = "output";
    public const string ClearCommandId = "output.clear";


    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IOutputService, OutputService>();
        services.AddSingleton<ILoggerProvider, OutputLoggerProvider>();
        services.AddSingleton<OutputViewModel>();
    }

    public void Contribute(IServiceProvider services)
    {
        var toolWindows = services.GetRequiredService<IToolWindowRegistry>();
        var commands = services.GetRequiredService<ICommandRegistry>();

        _registrations.Add(toolWindows.Register(new ToolWindowDefinition(
            ToolWindowId, Strings.ModuleName, IconNames.Output, ToolWindowLocation.Panel, services.GetRequiredService<OutputViewModel>)
        {
            Keybinding = "Ctrl+Shift+U",
        }));

        _registrations.Add(commands.Register(new CommandDefinition(
            ClearCommandId, Strings.Clear, (_, _) =>
            {
                services.GetRequiredService<OutputViewModel>().Clear();
                return ValueTask.CompletedTask;
            }, category: Strings.ModuleName)));
    }
}
