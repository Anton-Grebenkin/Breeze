namespace CodeEditor.Core.Context;

/// <summary>
/// Source of context key values for evaluating <c>when</c> conditions.
/// </summary>
public interface IContextKeyLookup
{
    /// <summary>Value of the key (<see cref="bool"/> or <see cref="string"/>); <c>null</c> when not set.</summary>
    object? GetValue(string key);
}
