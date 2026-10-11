using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>An agent edit: an exact file fragment and its replacement.</summary>
public sealed record FileEdit(
    [property: Description("File path relative to the workspace root.")] string Path,
    [property: Description("Exact text to replace; it must be unique in the file: include surrounding lines, or set line.")] string OldText,
    [property: Description("Replacement text.")] string NewText,
    [property: Description("Optional: the line where oldText starts, as read_file showed it; picks the occurrence when oldText appears more than once.")] int? Line = null)
{
    /// <summary>
    /// Anchor lines from <c>apply_patch</c> ("@@ class Order"): the fragment is searched below them, in order.
    /// Not part of the <c>apply_edits</c> schema, so models don't see them.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Anchors { get; init; } = [];
}
