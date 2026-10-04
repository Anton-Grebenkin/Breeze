using System.Reflection;
using CodeEditor.Architecture.Tests.Infrastructure;

namespace CodeEditor.Architecture.Tests;

/// <summary>
/// Checks layer dependency rules. If a test fails, fix the code, not the test.
/// </summary>
public sealed class LayerDependencyTests
{
    public static TheoryData<string> LogicAssemblies => [.. Layers.LogicAssemblies];

    public static TheoryData<string> PlatformAssemblies => [.. Layers.AllowedReferences.Keys];

    [Theory]
    [MemberData(nameof(LogicAssemblies))]
    public void LogicAssembly_DoesNotReferenceWpf(string assemblyName)
    {
        var wpfReferences = ReferencedNames(assemblyName)
            .Where(Layers.WpfAssemblies.Contains)
            .ToArray();

        Assert.True(wpfReferences.Length == 0,
            $"{assemblyName} ссылается на WPF: {string.Join(", ", wpfReferences)}. " +
            "Представления переносятся в сборку *.Wpf.");
    }

    [Theory]
    [MemberData(nameof(PlatformAssemblies))]
    public void PlatformAssembly_ReferencesOnlyAllowedLayers(string assemblyName)
    {
        var allowed = Layers.AllowedReferences[assemblyName];

        var forbidden = ReferencedNames(assemblyName)
            .Where(name => Layers.IsProductAssembly(name) && !allowed.Contains(name))
            .ToArray();

        Assert.True(forbidden.Length == 0,
            $"{assemblyName} ссылается на запрещённые сборки: {string.Join(", ", forbidden)}.");
    }

    private static IEnumerable<string> ReferencedNames(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);
}
