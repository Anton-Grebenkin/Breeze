using CodeEditor.Core.Context;
using CodeEditor.Modules.TextEditor.Commands;

namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>
/// Sets the "editor text focused" context key that gates <c>Ctrl+Z</c>, <c>Ctrl+Y</c> and clipboard commands.
/// </summary>
public sealed class EditorFocus(IContextKeyService context)
{
    public void SetFocused(bool isFocused) => context.Set(TextEditorCommands.EditorFocusContextKey, isFocused);
}
