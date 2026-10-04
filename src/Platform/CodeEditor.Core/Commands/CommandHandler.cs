namespace CodeEditor.Core.Commands;

/// <summary>
/// Command handler. The argument comes from a keybinding, a menu item or an agent call.
/// </summary>
public delegate ValueTask CommandHandler(object? argument, CancellationToken cancellationToken);
