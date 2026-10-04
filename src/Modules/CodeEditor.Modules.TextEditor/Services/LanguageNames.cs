using CodeEditor.Modules.TextEditor.Languages;
using CodeEditor.Modules.TextEditor.Resources;

namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>A file's status bar language name from the catalog (ADR 0036); plain text if unknown.</summary>
public static class LanguageNames
{
    public static string PlainText => Strings.PlainText;

    public static string ForFile(string filePath) => LanguageCatalog.ForFile(filePath)?.Name ?? PlainText;
}
