namespace CodeEditor.Modules.Agent.Services.Memory;

/// <summary>
/// Memory extraction prompt after a turn (ADR 0012): the helper model reads the turn and decides what is worth keeping
/// between chats. The agent model itself rarely writes notes, so extraction is a separate pass.
/// </summary>
public static class MemoryPrompts
{
    public const string System = """
        You extract long-term memory for a coding agent from one finished turn of its chat about a project. Another chat later starts with only the list of notes, so keep what saves the agent work next time.
        Keep a note only if an agent on a different task in this project would act differently or faster because of it. Notes nobody reads are forgotten after a month.
        Keep (at most 3 notes, usually 0 or 1):
        - a correction or preference the user stated — type feedback: the rule, then "Why:" and "How to apply:" lines;
        - a non-obvious project fact the agent had to discover with several searches or reads: where a mechanism lives, how parts connect, a convention the code does not state — type project;
        - where something outside the code lives (a service, a dashboard, a document) — type reference;
        - who the user is (role, expertise) — type user.
        Don't keep: what the code, git history or project rules already say plainly; the task itself, its plan or progress; file listings; anything only this chat needed; anything uncertain. Never keep the agent's diagnosis of a bug, a suspected cause or a conclusion about how something fails unless the user confirmed it or a test or a run proved it — a wrong note steers every later chat the same wrong way.
        A note has: name (lowercase latin words with dashes, 2–5 words; to extend or correct an existing note reuse its name instead of adding a similar one), type, description (one line: when the note matters), content (short, with path:line references where they help; at most 600 characters).
        Answer with a JSON array of notes and nothing else: [] when there is nothing worth keeping.
        Write descriptions and content in the language of the user's messages.
        """;

    public static string Request(string existingNotes, string turn) =>
        $"""
        Existing notes:
        {existingNotes}

        The turn:
        {turn}

        Notes to keep as a JSON array:
        """;
}
