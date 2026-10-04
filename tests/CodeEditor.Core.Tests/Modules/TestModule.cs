using CodeEditor.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Core.Tests.Modules;

/// <summary>
/// Test module whose dependencies and behavior come from the constructor.
/// </summary>
internal sealed class TestModule(
    string id,
    string[]? dependencies = null,
    Action<IServiceCollection>? configure = null,
    Action<IServiceProvider>? contribute = null) : IModule
{
    public ModuleInfo Info { get; } = new(id, id) { Dependencies = [.. dependencies ?? []] };

    public bool Contributed { get; private set; }

    public void ConfigureServices(IServiceCollection services) => configure?.Invoke(services);

    public void Contribute(IServiceProvider services)
    {
        contribute?.Invoke(services);
        Contributed = true;
    }

    public override string ToString() => Info.Id;
}
