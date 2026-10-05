namespace CodeEditor.Shell.Services;

/// <summary>
/// A notification button. It runs a command, so the same action is in the palette and works from the keyboard.
/// </summary>
/// <param name="IsPrimary">Shown as the main button, in the accent color.</param>
public sealed record NotificationAction(string Title, string CommandId, bool IsPrimary = false);
