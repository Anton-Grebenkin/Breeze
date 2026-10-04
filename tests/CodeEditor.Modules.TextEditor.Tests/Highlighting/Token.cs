namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>A highlighted span: text and theme token (<c>Brush.Syntax.Comment</c>…); <c>null</c> is the default.</summary>
internal readonly record struct Token(string Text, string? ThemeKey)
{
    public override string ToString() => $"{ThemeKey ?? "—"}: {Text}";
}
