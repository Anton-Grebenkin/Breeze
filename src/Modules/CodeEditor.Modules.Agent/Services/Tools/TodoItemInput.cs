using System.ComponentModel;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>A plan item in a <c>manage_todo</c> call; the schema the model sees.</summary>
public sealed record TodoItemInput(
    [property: Description("Stable id of the item, like '1', '2'.")] string Id,
    [property: Description("Short imperative title, 3–7 words.")] string Title,
    [property: Description("'pending', 'in_progress' or 'completed'.")] string Status);
