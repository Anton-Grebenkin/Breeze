using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Short span constructors for samples: <c>Comment("// x")</c>.</summary>
internal static class Tokens
{
    public static Token Comment(string text) => new(text, ThemeKeys.SyntaxComment);

    public static Token String(string text) => new(text, ThemeKeys.SyntaxString);

    public static Token Number(string text) => new(text, ThemeKeys.SyntaxNumber);

    public static Token Keyword(string text) => new(text, ThemeKeys.SyntaxKeyword);

    public static Token Control(string text) => new(text, ThemeKeys.SyntaxControlKeyword);

    public static Token Function(string text) => new(text, ThemeKeys.SyntaxFunction);

    public static Token Type(string text) => new(text, ThemeKeys.SyntaxType);

    public static Token Tag(string text) => new(text, ThemeKeys.SyntaxTag);

    public static Token Attribute(string text) => new(text, ThemeKeys.SyntaxAttribute);

    public static Token Variable(string text) => new(text, ThemeKeys.SyntaxVariable);

    public static Token Preprocessor(string text) => new(text, ThemeKeys.SyntaxPreprocessor);

    public static Token Text(string text) => new(text, ThemeKeys.EditorForeground);

    public static Token Error(string text) => new(text, ThemeKeys.ErrorForeground);

    public static Token Warning(string text) => new(text, ThemeKeys.WarningForeground);

    public static Token Success(string text) => new(text, ThemeKeys.SuccessForeground);
}
