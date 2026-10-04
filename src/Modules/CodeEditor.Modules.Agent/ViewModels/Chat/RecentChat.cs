namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>A recent chat shown on the panel's empty screen; clicking it opens the chat.</summary>
public sealed record RecentChat(string Id, string Title, string When);
