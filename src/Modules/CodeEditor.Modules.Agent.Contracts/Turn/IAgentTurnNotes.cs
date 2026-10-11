namespace CodeEditor.Modules.Agent.Contracts.Turn;

/// <summary>
/// Events the model should learn about mid-turn, such as a background command that finished. The notes are appended to
/// the next tool result: the model reads them at once, and history stays a prefix of every next request (cache).
/// </summary>
public interface IAgentTurnNotes
{
    /// <summary>Notes since the last call; each note is returned once.</summary>
    IReadOnlyList<string> TakeNotes();
}
