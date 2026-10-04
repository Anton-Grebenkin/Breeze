using System.ComponentModel;
using System.Text.Json;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>A cell value from the model: an address and a JSON value (text typed as in Excel, a number or a boolean).</summary>
public sealed record CellInput(
    [property: Description("The cell address, e.g. B2.")] string Cell,
    [property: Description("The value as typed in Excel: 12.5 is a number, TRUE or FALSE a boolean, =SUM(B2:B5) a formula, '00123 text; empty clears the cell.")] JsonElement Value);
