namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>Block style ids in a specific document (<see cref="WordStyles.Ensure"/>).</summary>
/// <param name="Headings">Headings 1–6 in order.</param>
internal sealed record WordStyleIds(IReadOnlyList<string> Headings, string ListParagraph, string Code, string Quote, string Hyperlink, string Table)
{
    public string Heading(int level) => Headings[Math.Clamp(level, 1, Headings.Count) - 1];
}
