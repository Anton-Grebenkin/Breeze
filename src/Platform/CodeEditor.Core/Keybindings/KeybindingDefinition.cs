using CodeEditor.Core.Context;

namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Binds a key gesture to a command, with an optional <c>when</c> condition and argument.
/// </summary>
/// <param name="DisplayOnly">
/// Only a hint in menus and the palette: the focused element handles the key itself (e.g. <c>Ctrl+C</c> in a text
/// box) and the router does not intercept it.
/// </param>
public sealed record KeybindingDefinition(
    KeySequence Sequence,
    string CommandId,
    ContextExpression? When = null,
    object? Argument = null,
    bool DisplayOnly = false);
