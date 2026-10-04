namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>
/// A language sample for highlighting checks: file name, text, and spans that must get a theme token
/// (<see cref="Expected"/>) or must not (<see cref="Unexpected"/>, e.g. <c>obj.type</c> is not a keyword).
/// </summary>
internal sealed record HighlightingSample(string FileName, string Text)
{
    public IReadOnlyList<Token> Expected { get; init; } = [];

    public IReadOnlyList<Token> Unexpected { get; init; } = [];
}
