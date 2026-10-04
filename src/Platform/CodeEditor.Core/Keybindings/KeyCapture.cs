namespace CodeEditor.Core.Keybindings;

/// <summary>
/// A focused element that takes keys for itself, like a terminal: while <paramref name="ContextKey"/> is set, only the
/// commands that <paramref name="KeepsKeys"/> accepts keep their keys, and every other key reaches the element
/// (<c>Ctrl+K</c>, <c>Ctrl+W</c>, <c>Ctrl+R</c> for the shell), as VS Code's "commands to skip shell".
/// </summary>
public sealed record KeyCapture(string ContextKey, Func<string, bool> KeepsKeys);
