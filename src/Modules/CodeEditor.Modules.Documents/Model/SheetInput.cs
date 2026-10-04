using System.ComponentModel;
using System.Text.Json;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>A sheet to write into a workbook: a name and rows of JSON values, as the model sends them.</summary>
public sealed record SheetInput(
    [property: Description("The sheet name, up to 31 characters, without : \\ / ? * [ ].")] string Name,
    [property: Description("Rows of cell values from A1, typed as in Excel: \"12.5\", \"TRUE\", \"=SUM(B2:B10)\", \"'00123\" for text; \"\" for an empty cell.")] JsonElement[][] Rows)
{
    /// <summary>The first row is a header: bold and frozen.</summary>
    [Description("The first row is a header: bold and frozen. Default true.")]
    public bool Header { get; init; } = true;
}
