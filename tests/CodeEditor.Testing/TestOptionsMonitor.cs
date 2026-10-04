using Microsoft.Extensions.Options;

namespace CodeEditor.Testing;

/// <summary>Test options monitor: <see cref="Set"/> changes the value and notifies listeners, like a file edit.</summary>
public sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    private readonly List<Action<T, string?>> _listeners = [];

    public T CurrentValue { get; private set; } = value;

    public T Get(string? name) => CurrentValue;

    public void Set(T value)
    {
        CurrentValue = value;
        foreach (var listener in _listeners.ToArray())
        {
            listener(value, Options.DefaultName);
        }
    }

    public IDisposable OnChange(Action<T, string?> listener)
    {
        _listeners.Add(listener);
        return new Subscription(() => _listeners.Remove(listener));
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
