using CodeEditor.Core.Context;

namespace CodeEditor.Core.Commands;

/// <summary>
/// Editor action with a unique id. Menus, keybindings, the command palette and agent tools are built from commands.
/// </summary>
public sealed class CommandDefinition
{
    public CommandDefinition(
        string id,
        string title,
        CommandHandler handler,
        string? category = null,
        ContextExpression? when = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(handler);

        Id = id;
        Title = title;
        Handler = handler;
        Category = category;
        When = when;
        DisplayTitle = category is null ? title : $"{category}: {title}";
    }

    /// <summary>Id of the form <c>area.action</c>, e.g. <c>file.save</c>.</summary>
    public string Id { get; }

    public string Title { get; }

    public string? Category { get; }

    /// <summary>Palette title: "Category: Title".</summary>
    public string DisplayTitle { get; }

    /// <summary>Enablement condition; <c>null</c> means always enabled.</summary>
    public ContextExpression? When { get; }

    public CommandHandler Handler { get; }

    public override string ToString() => Id;
}
