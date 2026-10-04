namespace CodeEditor.Shell.ViewModels;

/// <summary>A recent folder on the welcome page: prominent name, muted path, as in VS Code.</summary>
public sealed record RecentFolderItem(string Name, string Path);
