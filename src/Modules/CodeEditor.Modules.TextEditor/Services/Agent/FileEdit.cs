using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>An agent edit: an exact file fragment and its replacement.</summary>
public sealed record FileEdit(
    [property: Description("File path relative to the workspace root.")] string Path,
    [property: Description("Exact text to replace; must occur exactly once in the file. Include surrounding lines to make it unique.")] string OldText,
    [property: Description("Replacement text.")] string NewText)
{
    /// <summary>
    /// Anchor lines from <c>apply_patch</c> ("@@ class Order"): the fragment is searched below them, in order.
    /// Not part of the <c>apply_edits</c> schema, so models don't see them.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Anchors { get; init; } = [];
}
