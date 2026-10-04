using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Modules;

/// <summary>
/// Loads catalog modules in two phases: service registration and contributions.
/// A failing module is logged and its dependents are skipped; the rest keep working.
/// </summary>
public sealed partial class ModuleLoader(ModuleCatalog catalog, ILogger<ModuleLoader> logger)
{
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private readonly List<IModule> _configured = [];

    /// <summary>Modules rejected by the catalog, failed, or skipped because of a dependency.</summary>
    public IReadOnlySet<string> FailedModuleIds => _failed;

    /// <summary>Registers module services. A failed module's services never reach the container.</summary>
    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var rejection in catalog.Rejected)
        {
            LogRejected(logger, rejection.ModuleId, rejection.Reason, rejection.RelatedModuleId);
            _failed.Add(rejection.ModuleId);
        }

        foreach (var module in catalog.Modules)
        {
            // Stage first, so a partial registration does not stay in the container after an exception.
            var staging = new ServiceCollection();
            if (TryRun(module, () => module.ConfigureServices(staging)))
            {
                foreach (var descriptor in staging)
                {
                    services.Add(descriptor);
                }

                _configured.Add(module);
            }
        }
    }

    /// <summary>Runs contributions of modules that passed <see cref="ConfigureServices"/>.</summary>
    public void Contribute(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var module in _configured)
        {
            TryRun(module, () => module.Contribute(services));
        }
    }

    private bool TryRun(IModule module, Action action)
    {
        var failedDependency = module.Info.Dependencies.FirstOrDefault(_failed.Contains);
        if (failedDependency is not null)
        {
            LogSkipped(logger, module.Info.Id, failedDependency);
            _failed.Add(module.Info.Id);
            return false;
        }

        try
        {
            action();
            return true;
        }
#pragma warning disable CA1031 // By design: a module failure is isolated and must not crash the app.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogFailed(logger, exception, module.Info.Id);
            _failed.Add(module.Info.Id);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Module {ModuleId} rejected: {Reason} {RelatedModuleId}")]
    private static partial void LogRejected(ILogger logger, string moduleId, ModuleRejectionReason reason, string? relatedModuleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Module {ModuleId} failed to load")]
    private static partial void LogFailed(ILogger logger, Exception exception, string moduleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Module {ModuleId} skipped: dependency {DependencyId} failed to load")]
    private static partial void LogSkipped(ILogger logger, string moduleId, string dependencyId);
}
