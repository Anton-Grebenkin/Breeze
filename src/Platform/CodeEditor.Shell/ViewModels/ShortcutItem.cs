namespace CodeEditor.Shell.ViewModels;

/// <summary>A welcome page row: a command and its keybinding; clicking runs the command.</summary>
public sealed record ShortcutItem(string CommandId, string Title, string Keys);
