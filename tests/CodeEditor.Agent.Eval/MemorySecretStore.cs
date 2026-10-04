using CodeEditor.Core.Storage;

namespace CodeEditor.Agent.Eval;

/// <summary>Keeps the run's API key in memory, so the user's secret store is only read.</summary>
internal sealed class MemorySecretStore(string name, string value) : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal) { [name] = value };

    public string? Get(string name) => _values.GetValueOrDefault(name);

    public void Set(string name, string value) => _values[name] = value;

    public void Remove(string name) => _values.Remove(name);
}
