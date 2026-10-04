namespace CodeEditor.Modules.Agent.Services.Memory;

/// <summary>A folder memory note (<see cref="ProjectMemory"/>): file name, type, a "when it matters" line, date and text.</summary>
/// <param name="Type">user, feedback, project or reference.</param>
public sealed record MemoryNote(string Name, string Type, string Description, DateOnly Updated, string Content)
{
    /// <summary>
    /// Written by the helper model after a turn (<see cref="MemoryExtractor"/>) rather than by the agent while working or
    /// at the user's request. Such a note is forgotten if unread for long (ADR 0029).
    /// </summary>
    public bool IsAutomatic { get; init; }

    /// <summary>When the agent last read or updated the note.</summary>
    public DateOnly Used { get; init; }

    /// <summary>How many times the agent read the note.</summary>
    public int Uses { get; init; }
}
