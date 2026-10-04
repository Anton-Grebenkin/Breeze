using System.Collections.Concurrent;
using CodeEditor.Modules.Diagrams.Services.Agent;

namespace CodeEditor.Modules.Diagrams.Services.Export;

/// <summary>
/// Files the diagram export wrote during this editor session. The agent overwrites them without asking (its own earlier
/// export); a foreign file with the same name needs a card first (<see cref="DiagramApprovals"/>).
/// </summary>
public sealed class DiagramWrites
{
    private readonly ConcurrentDictionary<string, byte> _paths = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string path) => _paths[Path.GetFullPath(path)] = 0;

    public bool Contains(string path) => _paths.ContainsKey(Path.GetFullPath(path));
}
