using System.Collections.Immutable;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>
/// A document block: paragraph, heading, list item, table, code, quote, rule, slide or notes. Text is styled runs
/// (<see cref="Runs"/>); a table has <see cref="Table"/>.
/// </summary>
public sealed record DocumentBlock(DocumentBlockKind Kind, ImmutableArray<TextRun> Runs)
{
    /// <summary>Heading level 1–6; list item nesting level from 0.</summary>
    public int Level { get; init; }

    /// <summary>The list item belongs to a numbered list.</summary>
    public bool IsOrdered { get; init; }

    /// <summary>Number of a numbered list item or a slide.</summary>
    public int Number { get; init; }

    public BlockAlignment Alignment { get; init; }

    public DocumentTable? Table { get; init; }

    /// <summary>Block text without styles.</summary>
    public string PlainText => string.Concat(Runs.Select(run => run.Text));

    public static DocumentBlock Of(DocumentBlockKind kind, string text) => new(kind, [new TextRun(text)]);
}
