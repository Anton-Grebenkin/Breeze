using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.TextEditor.Resources;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Editor tool rows in the agent feed (ADR 0010), e.g. "Edited Order.cs · +3 −1", "Created Tests.cs · +40",
/// "Open files", "Editor selection". Line counts come from the edit fragments, not the whole file.
/// </summary>
public sealed class EditorToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name switch
        {
            EditingAgentTools.ApplyEditsName => Edits(call),
            EditingAgentTools.ApplyPatchName => Patch(call),
            EditingAgentTools.CreateFileName => Create(call),
            EditorAgentTools.OpenDocumentsName => new AgentToolView(AgentToolIcon.Read, Strings.OpenDocumentsTitle) { IsExploration = true },
            EditorAgentTools.SelectionName => new AgentToolView(AgentToolIcon.Read, Strings.SelectionTitle) { IsExploration = true },
            _ => null,
        };
    }

    private static AgentToolView Edits(AgentToolCall call)
    {
        var edits = call.Items("edits");
        var files = edits.Select(edit => Property(edit, "path")).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var target = files.Count == 1 ? ToolText.FileName(files[0]) : Plural.Format(files.Count, Strings.FileForms);
        var (added, removed) = edits.Aggregate((Added: 0, Removed: 0), (sum, edit) =>
        {
            var (plus, minus) = LineDiff.Count(Property(edit, "oldText") ?? string.Empty, Property(edit, "newText") ?? string.Empty);
            return (sum.Added + plus, sum.Removed + minus);
        });
        return new AgentToolView(AgentToolIcon.Edit, Format(call.IsDone ? Strings.EditedTitle : Strings.EditingTitle, target))
        {
            FilePath = files.Count > 0 ? files[0] : null,
            Detail = $"+{added} −{removed}",
        };
    }

    // V4A patch: files from "*** Update/Add File: path" headers, line counts from "+" and "-" prefixes.
    private static AgentToolView Patch(AgentToolCall call)
    {
        var lines = LineDiff.Lines(call.Text("input") ?? string.Empty);
        var files = lines
            .Where(line => line.StartsWith("*** Update File:", StringComparison.Ordinal) || line.StartsWith("*** Add File:", StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var target = files.Count == 1 ? ToolText.FileName(files[0]) : Plural.Format(files.Count, Strings.FileForms);
        return new AgentToolView(AgentToolIcon.Edit, Format(call.IsDone ? Strings.EditedTitle : Strings.EditingTitle, target))
        {
            FilePath = files.Count > 0 ? files[0] : null,
            Detail = $"+{lines.Count(line => line.StartsWith('+'))} −{lines.Count(line => line.StartsWith('-'))}",
        };
    }

    private static AgentToolView Create(AgentToolCall call)
    {
        var path = call.Text("path") ?? "?";
        var lines = LineDiff.Lines(call.Text("content") ?? string.Empty).Length;
        return new AgentToolView(AgentToolIcon.Create, Format(call.IsDone ? Strings.CreatedTitle : Strings.CreatingTitle, ToolText.FileName(path)))
        {
            FilePath = path,
            Detail = $"+{lines}",
        };
    }

    private static string Format(string format, string target) => string.Format(CultureInfo.CurrentCulture, format, target);

    private static string? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
