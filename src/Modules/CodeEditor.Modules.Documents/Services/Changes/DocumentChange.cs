using System.Text.Json;
using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>A <c>document_change</c> call: action, document and action arguments (<see cref="DocumentActions"/>).</summary>
public sealed record DocumentChange(string Action, string Path)
{
    /// <summary>create_docx, create_pdf, insert: content in Markdown.</summary>
    public string? Markdown { get; init; }

    /// <summary>create_xlsx: sheets with data.</summary>
    public IReadOnlyList<SheetInput> Sheets { get; init; } = [];

    /// <summary>replace_text: text to find.</summary>
    public string? Find { get; init; }

    /// <summary>replace_text: replacement text.</summary>
    public string? Replace { get; init; }

    /// <summary>replace_text: every occurrence instead of a single one.</summary>
    public bool All { get; init; }

    /// <summary>insert: the paragraph to insert after; <c>null</c> appends at the end.</summary>
    public string? After { get; init; }

    /// <summary>set_cells, add_sheet, rename_sheet: the sheet.</summary>
    public string? Sheet { get; init; }

    /// <summary>set_cells: values by address.</summary>
    public IReadOnlyList<CellInput> Cells { get; init; } = [];

    /// <summary>set_cells, add_sheet: rows of values from cell <see cref="Start"/>.</summary>
    public JsonElement[][] Rows { get; init; } = [];

    /// <summary>set_cells: top-left cell for <see cref="Rows"/>; <c>null</c> means A1.</summary>
    public string? Start { get; init; }

    /// <summary>rename_sheet: the new name.</summary>
    public string? NewName { get; init; }

    /// <summary>merge_pdf: sources in order.</summary>
    public IReadOnlyList<PdfSourceInput> Sources { get; init; } = [];

    /// <summary>extract_pages: the source PDF.</summary>
    public string? Source { get; init; }

    /// <summary>extract_pages: the pages.</summary>
    public string? Pages { get; init; }

    /// <summary>Create over an existing file.</summary>
    public bool Overwrite { get; init; }
}
