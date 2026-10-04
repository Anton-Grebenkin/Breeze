using System.Collections.Immutable;

namespace CodeEditor.Core.Modules;

/// <summary>
/// Module description: unique id, display name and ids of the modules it depends on.
/// </summary>
public sealed record ModuleInfo(string Id, string Name)
{
    public ImmutableArray<string> Dependencies { get; init; } = [];
}
