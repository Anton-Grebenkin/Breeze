using System.ComponentModel;
using System.Globalization;
using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Memory;

/// <summary>
/// The <c>memory</c> tool (ADR 0012): read folder memory notes by name, save or delete a note. One tool with an action
/// means fewer schemas in the request. No approval, but shown as a feed line; available in every mode, so "remember"
/// works in Ask mode too.
/// </summary>
public sealed class MemoryAgentTools(ProjectMemory memory) : IAgentToolProvider
{
    public const string MemoryName = "memory";

    public IEnumerable<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(Memory, MemoryName,
            "Folder memory kept between chats; the first message of a chat lists the notes. " +
            "action 'read' returns notes by name (several names separated by commas); 'save' creates or replaces a note; 'delete' removes one. " +
            "Save when the user corrects you or states a preference (type feedback: the rule, then 'Why:' and 'How to apply:' lines), " +
            "or when you learn a non-obvious project fact that the code and project rules don't show (type project), where something outside the code lives (type reference), or who the user is (type user)."),
    ];

    private string Memory(
        [Description("'read', 'save' or 'delete'.")] string action,
        [Description("Note name: lowercase latin words with dashes, like 'build-commands'. For read, several names separated by commas.")] string name,
        [Description("For save: user, feedback, project or reference.")] string? type = null,
        [Description("For save: one line that says when the note matters.")] string? description = null,
        [Description("For save: the note itself, short.")] string? content = null) => action?.Trim().ToLowerInvariant() switch
    {
        "read" => Read(name),
        "save" => Save(name?.Trim() ?? string.Empty, Required(type, nameof(type)), Required(description, nameof(description)), Required(content, nameof(content))),
        "delete" => memory.Delete(name?.Trim() ?? string.Empty) ? Format(Strings.MemoryDeleted, name!) : Format(Strings.MemoryNotFound, name ?? string.Empty),
        _ => throw new AgentToolException(Strings.MemoryBadAction),
    };

    private string Read(string names)
    {
        var text = new StringBuilder();
        foreach (var name in (names ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            text.Append(memory.Use(name) is { } note
                ? string.Create(CultureInfo.InvariantCulture, $"## {note.Name} ({note.Type}, {note.Updated:yyyy-MM-dd})\n{note.Content}\n\n")
                : Format(Strings.MemoryNotFound, name) + "\n\n");
        }

        return text.Length == 0 ? Strings.MemoryBadName : text.ToString().TrimEnd();
    }

    private string Save(string name, string type, string description, string content) =>
        Format(memory.Save(name, type.Trim().ToLowerInvariant(), description, content) ? Strings.MemorySaved : Strings.MemoryUpdated, name);

    private static string Required(string? value, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? throw new AgentToolException(Format(Strings.MemoryMissingField, parameter)) : value;

    private static string Format(string format, string value) => string.Format(CultureInfo.CurrentCulture, format, value);
}
