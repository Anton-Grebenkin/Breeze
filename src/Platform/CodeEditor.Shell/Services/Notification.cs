namespace CodeEditor.Shell.Services;

/// <summary>A message with buttons at the bottom left of the window, as in VS Code and Cursor.</summary>
/// <param name="Id">The card's automation id is <c>Notification.{Id}</c>, a button's <c>Notification.{Id}.{CommandId}</c>.</param>
public sealed record Notification(string Id, string Message, IReadOnlyList<NotificationAction> Actions);
