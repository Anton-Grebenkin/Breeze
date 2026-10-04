namespace CodeEditor.Modules.Terminal.ViewModels;

/// <summary>The terminal's colors from the theme tokens, as CSS colors; the page picks the ANSI palette by scheme.</summary>
/// <param name="Scheme"><c>dark</c> or <c>light</c>.</param>
public sealed record TerminalTheme(string Scheme, string Background, string Foreground, string Cursor, string Selection);
