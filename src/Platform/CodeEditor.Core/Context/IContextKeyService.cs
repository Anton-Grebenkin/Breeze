namespace CodeEditor.Core.Context;

/// <summary>
/// Context flags and values (<c>editorFocus</c>, <c>workspaceOpen</c>, <c>activePanel</c>) used to evaluate
/// the <c>when</c> conditions of commands, keybindings and menus.
/// </summary>
public interface IContextKeyService : IContextKeyLookup
{
    /// <summary>A key was added, changed or removed.</summary>
    event EventHandler<ContextKeyChangedEventArgs>? Changed;

    void Set(string key, bool value);

    /// <summary>Sets a string value; <c>null</c> removes the key.</summary>
    void Set(string key, string? value);

    void Remove(string key);

    /// <summary>Evaluates the condition in the current context; a missing condition is true.</summary>
    bool Evaluate(ContextExpression? expression);
}
