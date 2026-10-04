using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// <c>document_change</c> arguments from the model's dictionary, for the approval card before the call itself. An empty
/// insert anchor, sheet or start cell means "not set", the same as in the call.
/// </summary>
public static class DocumentArguments
{
    /// <exception cref="AgentToolException">No action or path, or an argument has the wrong type.</exception>
    public static DocumentChange Change(IDictionary<string, object?> arguments) =>
        new(ToolArguments.Get<string>(arguments, "action"), ToolArguments.Get<string>(arguments, "path"))
        {
            Markdown = ToolArguments.GetOptional<string>(arguments, "markdown"),
            Sheets = ToolArguments.GetOptional<SheetInput[]>(arguments, "sheets") ?? [],
            Find = ToolArguments.GetOptional<string>(arguments, "find"),
            Replace = ToolArguments.GetOptional<string>(arguments, "replace"),
            All = ToolArguments.GetOptional<bool>(arguments, "all"),
            After = Blank(ToolArguments.GetOptional<string>(arguments, "after")),
            Sheet = Blank(ToolArguments.GetOptional<string>(arguments, "sheet")),
            Cells = ToolArguments.GetOptional<CellInput[]>(arguments, "cells") ?? [],
            Rows = ToolArguments.GetOptional<JsonElement[][]>(arguments, "rows") ?? [],
            Start = Blank(ToolArguments.GetOptional<string>(arguments, "start")),
            NewName = ToolArguments.GetOptional<string>(arguments, "newName"),
            Sources = ToolArguments.GetOptional<PdfSourceInput[]>(arguments, "sources") ?? [],
            Source = ToolArguments.GetOptional<string>(arguments, "source"),
            Pages = ToolArguments.GetOptional<string>(arguments, "pages"),
            Overwrite = ToolArguments.GetOptional<bool>(arguments, "overwrite"),
        };

    /// <summary>Returns <c>null</c> for an empty or whitespace string, otherwise the trimmed string.</summary>
    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
