using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Core.Modules;

/// <summary>
/// Editor module. It does not insert itself into the UI; it declares contributions instead:
/// commands, keybindings, panels, settings, agent tools.
/// </summary>
public interface IModule
{
    ModuleInfo Info { get; }

    /// <summary>Registers the module's services. Called before the container is built.</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Adds contributions to registries resolved from the container. Called in dependency order.</summary>
    void Contribute(IServiceProvider services);
}
