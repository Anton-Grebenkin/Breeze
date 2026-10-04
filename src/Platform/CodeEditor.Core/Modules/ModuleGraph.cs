namespace CodeEditor.Core.Modules;

/// <summary>
/// Module dependency graph over indexes. Edge i → j means "module i depends on j".
/// </summary>
internal sealed class ModuleGraph
{
    private readonly IReadOnlyList<IModule> _modules;
    private readonly int[][] _dependencies;
    private readonly List<int>[] _dependents;
    private readonly string?[] _missingDependency;

    private ModuleGraph(IReadOnlyList<IModule> modules)
    {
        _modules = modules;
        _dependencies = new int[modules.Count][];
        _dependents = new List<int>[modules.Count];
        _missingDependency = new string?[modules.Count];
    }

    public static ModuleGraph Build(IReadOnlyList<IModule> modules, IReadOnlyDictionary<string, int> indexById)
    {
        var graph = new ModuleGraph(modules);
        for (var index = 0; index < modules.Count; index++)
        {
            graph._dependents[index] = [];
        }

        for (var index = 0; index < modules.Count; index++)
        {
            var dependencies = new List<int>();
            foreach (var dependencyId in modules[index].Info.Dependencies.Distinct(StringComparer.Ordinal))
            {
                if (indexById.TryGetValue(dependencyId, out var dependency))
                {
                    dependencies.Add(dependency);
                    graph._dependents[dependency].Add(index);
                }
                else
                {
                    graph._missingDependency[index] ??= dependencyId;
                }
            }

            graph._dependencies[index] = [.. dependencies];
        }

        return graph;
    }

    /// <summary>
    /// Kahn's algorithm. Modules with missing dependencies or on cycles (and their dependents) are not emitted.
    /// </summary>
    public IEnumerable<int> TopologicalOrder()
    {
        var remaining = new int[_modules.Count];
        var ready = new PriorityQueue<int, int>();

        for (var index = 0; index < _modules.Count; index++)
        {
            remaining[index] = _dependencies[index].Length;
            if (remaining[index] == 0 && _missingDependency[index] is null)
            {
                ready.Enqueue(index, index);
            }
        }

        while (ready.TryDequeue(out var index, out _))
        {
            yield return index;

            foreach (var dependent in _dependents[index])
            {
                if (--remaining[dependent] == 0 && _missingDependency[dependent] is null)
                {
                    ready.Enqueue(dependent, dependent);
                }
            }
        }
    }

    /// <summary>Explains why a module is missing from the load order.</summary>
    public ModuleRejection ExplainRejection(int index, bool[] emitted)
    {
        var id = _modules[index].Info.Id;

        if (_missingDependency[index] is { } missing)
        {
            return new ModuleRejection(id, ModuleRejectionReason.MissingDependency, missing);
        }

        if (IsOnCycle(index, emitted))
        {
            return new ModuleRejection(id, ModuleRejectionReason.CircularDependency);
        }

        var blocker = _dependencies[index].First(dependency => !emitted[dependency]);
        return new ModuleRejection(id, ModuleRejectionReason.DependencyRejected, _modules[blocker].Info.Id);
    }

    /// <summary>
    /// Whether the module is reachable from itself through non-emitted modules. O(V + E) per call;
    /// called only for the few rejected modules.
    /// </summary>
    private bool IsOnCycle(int start, bool[] emitted)
    {
        var visited = new bool[_modules.Count];
        var stack = new Stack<int>(_dependencies[start]);

        while (stack.TryPop(out var current))
        {
            if (current == start)
            {
                return true;
            }

            if (emitted[current] || visited[current])
            {
                continue;
            }

            visited[current] = true;
            foreach (var dependency in _dependencies[current])
            {
                stack.Push(dependency);
            }
        }

        return false;
    }
}
