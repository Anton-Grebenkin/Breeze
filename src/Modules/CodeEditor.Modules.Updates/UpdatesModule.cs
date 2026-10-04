using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Updates.Commands;
using CodeEditor.Modules.Updates.Resources;
using CodeEditor.Modules.Updates.Services;
using CodeEditor.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Updates;

/// <summary>
/// Updates of the installed app (ADR 0043): Velopack checks the GitHub releases of the repository the build came
/// from, downloads in the background and installs on restart. Builds run from the IDE skip it.
/// </summary>
public sealed class UpdatesModule : IModule
{
    public const string Id = "updates";

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsSection<UpdateSettings>(UpdateSettings.Section);
        services.AddSingleton<IAppUpdater, VelopackUpdater>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<UpdateCommands>();
    }

    public void Contribute(IServiceProvider services)
    {
        services.GetRequiredService<UpdateCommands>().Register(services.GetRequiredService<ICommandRegistry>(), services.GetRequiredService<IMenuRegistry>());
        services.GetRequiredService<UpdateService>().Start();
    }
}
