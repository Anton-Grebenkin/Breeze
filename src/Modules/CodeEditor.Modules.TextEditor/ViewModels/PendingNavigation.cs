using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.TextEditor.ViewModels;

/// <summary>A request to the editor view: where to go (<c>null</c> to stay) and whether to take focus.</summary>
public sealed record PendingNavigation(EditorLocation? Location, bool Focus);
