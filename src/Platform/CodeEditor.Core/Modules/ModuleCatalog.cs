using System.Collections.Immutable;

namespace CodeEditor.Core.Modules;

/// <summary>
/// Validated set of modules in dependency order. Invalid modules do not crash the app; they go to
/// <see cref="Rejected"/> together with their dependents.
/// </summary>
public sealed class ModuleCatalog
{
    private ModuleCatalog(ImmutableArray<IModule> modules, ImmutableArray<ModuleRejection> rejected)
    {
        Modules = modules;
        Rejected = rejected;
    }

    /// <summary>Modules ordered so that dependencies come first.</summary>
    public ImmutableArray<IModule> Modules { get; }

    public ImmutableArray<ModuleRejection> Rejected { get; }

    /// <summary>
    /// Builds the catalog with Kahn's sort in O((V + E) log V); ties keep the input order.
    /// </summary>
    public static ModuleCatalog Create(IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var rejected = ImmutableArray.CreateBuilder<ModuleRejection>();
        var unique = RemoveDuplicates(modules, rejected, out var indexById);
        var graph = ModuleGraph.Build(unique, indexById);

        var emitted = new bool[unique.Count];
        var ordered = ImmutableArray.CreateBuilder<IModule>(unique.Count);
        foreach (var index in graph.TopologicalOrder())
        {
            emitted[index] = true;
            ordered.Add(unique[index]);
        }

        for (var index = 0; index < unique.Count; index++)
        {
            if (!emitted[index])
            {
                rejected.Add(graph.ExplainRejection(index, emitted));
            }
        }

        return new ModuleCatalog(ordered.ToImmutable(), rejected.ToImmutable());
    }

    private static List<IModule> RemoveDuplicates(
        IEnumerable<IModule> modules,
        ImmutableArray<ModuleRejection>.Builder rejected,
        out Dictionary<string, int> indexById)
    {
        var unique = new List<IModule>();
        indexById = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            if (indexById.TryAdd(module.Info.Id, unique.Count))
            {
                unique.Add(module);
            }
            else
            {
                rejected.Add(new ModuleRejection(module.Info.Id, ModuleRejectionReason.DuplicateId));
            }
        }

        return unique;
    }
}
