using System.Runtime.InteropServices;

namespace CodeEditor.Core.Context;

/// <summary>
/// Context key store. Not thread-safe: used from the UI thread.
/// </summary>
public sealed class ContextKeyService : IContextKeyService
{
    // Pre-boxed values: setting a flag does not allocate.
    private static readonly object TrueValue = true;
    private static readonly object FalseValue = false;

    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public event EventHandler<ContextKeyChangedEventArgs>? Changed;

    public object? GetValue(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, bool value) => SetValue(key, value ? TrueValue : FalseValue);

    public void Set(string key, string? value)
    {
        if (value is null)
        {
            Remove(key);
            return;
        }

        SetValue(key, value);
    }

    public void Remove(string key)
    {
        if (_values.Remove(key))
        {
            OnChanged(key);
        }
    }

    public bool Evaluate(ContextExpression? expression) => expression is null || expression.Evaluate(this);

    private void SetValue(string key, object value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_values, key, out var exists);
        if (exists && Equals(slot, value))
        {
            return;
        }

        slot = value;
        OnChanged(key);
    }

    private void OnChanged(string key) => Changed?.Invoke(this, new ContextKeyChangedEventArgs(key));
}
