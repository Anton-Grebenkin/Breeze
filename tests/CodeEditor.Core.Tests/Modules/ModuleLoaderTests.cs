using CodeEditor.Core.Modules;
using CodeEditor.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Core.Tests.Modules;

public sealed class ModuleLoaderTests
{
    private readonly CollectingLogger<ModuleLoader> _logger = new();
    private readonly ServiceCollection _services = new();

    [Fact]
    public void ConfigureServices_FailingModule_LeavesNoPartialRegistrations()
    {
        var broken = new TestModule("broken", configure: services =>
        {
            services.AddSingleton<PartialService>();
            throw new InvalidOperationException("сбой");
        });
        var healthy = new TestModule("healthy", configure: services => services.AddSingleton<HealthyService>());

        var loader = Load(broken, healthy);

        Assert.DoesNotContain(_services, descriptor => descriptor.ServiceType == typeof(PartialService));
        Assert.Contains(_services, descriptor => descriptor.ServiceType == typeof(HealthyService));
        Assert.Equal(["broken"], loader.FailedModuleIds);
        Assert.Single(_logger.Entries, entry => entry.Exception is InvalidOperationException);
    }

    [Fact]
    public void FailingModule_SkipsDependents()
    {
        var broken = new TestModule("explorer", configure: _ => throw new InvalidOperationException("сбой"));
        var dependent = new TestModule("agent", ["explorer"]);
        var independent = new TestModule("search");

        var loader = Load(broken, dependent, independent);

        Assert.False(dependent.Contributed);
        Assert.True(independent.Contributed);
        Assert.Equal(["agent", "explorer"], loader.FailedModuleIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Contribute_FailingModule_IsIsolated()
    {
        var broken = new TestModule("broken", contribute: _ => throw new InvalidOperationException("сбой"));
        var dependent = new TestModule("dependent", ["broken"]);
        var independent = new TestModule("independent");

        var loader = Load(broken, dependent, independent);

        Assert.False(dependent.Contributed);
        Assert.True(independent.Contributed);
        Assert.Contains("broken", loader.FailedModuleIds);
        Assert.Contains("dependent", loader.FailedModuleIds);
    }

    [Fact]
    public void Contribute_ReceivesServicesFromContainer()
    {
        HealthyService? resolved = null;
        var module = new TestModule(
            "healthy",
            configure: services => services.AddSingleton<HealthyService>(),
            contribute: provider => resolved = provider.GetService<HealthyService>());

        Load(module);

        Assert.NotNull(resolved);
    }

    [Fact]
    public void RejectedModules_AreReportedAsFailed()
    {
        var loader = Load(new TestModule("git", ["terminal"]));

        Assert.Equal(["git"], loader.FailedModuleIds);
    }

    private ModuleLoader Load(params IModule[] modules)
    {
        var loader = new ModuleLoader(ModuleCatalog.Create(modules), _logger);
        loader.ConfigureServices(_services);

        using var provider = _services.BuildServiceProvider();
        loader.Contribute(provider);
        return loader;
    }

    private sealed class PartialService;

    private sealed class HealthyService;
}
