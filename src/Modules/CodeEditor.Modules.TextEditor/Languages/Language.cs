using System.Collections.Immutable;

namespace CodeEditor.Modules.TextEditor.Languages;

/// <summary>
/// A file language (ADR 0036): its status bar name, the file names it is recognized by, and its highlighting
/// definition. All languages live in <see cref="LanguageCatalog"/>.
/// </summary>
/// <param name="Id">VS Code id, e.g. <c>typescript</c>, <c>shellscript</c>; also valid as a Markdown fence language.</param>
/// <param name="Name">Status bar name, e.g. "TypeScript"; a format name, not translated (as in VS Code).</param>
/// <param name="Highlighting">Highlighting definition name: built into AvalonEdit ("C#", "XML") or custom
/// (<c>Highlighting/Definitions/*.xshd</c> in <c>TextEditor.Wpf</c>).</param>
public sealed record Language(string Id, string Name, string Highlighting)
{
    /// <summary>Extensions with the dot: <c>.ts</c>.</summary>
    public ImmutableArray<string> Extensions { get; init; } = [];

    /// <summary>Exact file names: <c>Dockerfile</c>, <c>.gitignore</c>, <c>CMakeLists.txt</c>.</summary>
    public ImmutableArray<string> FileNames { get; init; } = [];

    /// <summary>Wildcard name patterns: <c>Dockerfile.*</c>, <c>.env.*</c>.</summary>
    public ImmutableArray<string> FilePatterns { get; init; } = [];

    /// <summary>Other Markdown fence names besides <see cref="Id"/> and extensions: <c>golang</c>, <c>bash</c>.</summary>
    public ImmutableArray<string> Aliases { get; init; } = [];
}
