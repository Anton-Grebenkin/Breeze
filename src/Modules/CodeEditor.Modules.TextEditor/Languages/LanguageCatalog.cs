using System.Collections.Frozen;
using System.Collections.Immutable;
using System.IO.Enumeration;

namespace CodeEditor.Modules.TextEditor.Languages;

/// <summary>
/// Language catalog (ADR 0036): the single source for the status bar, editor highlighting and code blocks in agent
/// replies. A file's language is found by exact name (<c>Dockerfile</c>), then pattern (<c>Dockerfile.*</c>), then
/// extension, as in VS Code; case-insensitive, as on Windows.
/// </summary>
/// <remarks>Name and extension lookups are O(1); the few patterns are scanned linearly.</remarks>
public static class LanguageCatalog
{
    private static readonly FrozenDictionary<string, Language> ByFileName = Index(language => language.FileNames);
    private static readonly FrozenDictionary<string, Language> ByExtension = Index(language => language.Extensions);
    private static readonly FrozenDictionary<string, Language> ByAlias = Index(language => [language.Id, .. language.Aliases]);

    private static readonly ImmutableArray<(string Pattern, Language Language)> ByPattern =
        [.. KnownLanguages.All.SelectMany(language => language.FilePatterns.Select(pattern => (pattern, language)))];

    public static ImmutableArray<Language> All => KnownLanguages.All;

    /// <summary>The file's language, or <c>null</c> for an unknown format (plain text).</summary>
    public static Language? ForFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var name = Path.GetFileName(filePath);
        return ByFileName.GetValueOrDefault(name)
            ?? ByPatternMatch(name)
            ?? ByExtension.GetValueOrDefault(Path.GetExtension(name));
    }

    /// <summary>
    /// Language of a Markdown fence by its info word: id (<c>typescript</c>), alias (<c>bash</c>), extension
    /// (<c>rs</c>) or a dot-less file name (<c>gitignore</c>).
    /// </summary>
    public static Language? ForAlias(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        return ByAlias.GetValueOrDefault(alias) ?? ForFile("." + alias);
    }

    private static Language? ByPatternMatch(string fileName)
    {
        foreach (var (pattern, language) in ByPattern)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, fileName))
            {
                return language;
            }
        }

        return null;
    }

    // A duplicate key would silently replace a language; tests guard the table.
    private static FrozenDictionary<string, Language> Index(Func<Language, IEnumerable<string>> keys) =>
        KnownLanguages.All
            .SelectMany(language => keys(language).Select(key => KeyValuePair.Create(key, language)))
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}
