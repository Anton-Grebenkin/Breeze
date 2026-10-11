using CodeEditor.Modules.Agent.Contracts.Turn;

namespace CodeEditor.Modules.Agent.Services.Watchdog;

/// <summary>
/// What the editor tells the model between tool calls: notes of the turn watchdog (<see cref="TurnWatchdog"/>) and
/// module events such as a finished background command (<see cref="IAgentTurnNotes"/>). They go into the tool result as
/// an editor note, so the model reads them at once and history stays the prefix of the next request (cache).
/// </summary>
public sealed class ToolCallObservers(TurnWatchdog watchdog, IEnumerable<IAgentTurnNotes> modules)
{
    /// <summary>
    /// A new turn: the watchdog's streaks start over. Notes left from between turns are dropped: the message's context
    /// already shows the state of background commands.
    /// </summary>
    public void Begin()
    {
        watchdog.Begin();
        foreach (var module in modules)
        {
            module.TakeNotes();
        }
    }

    /// <returns>Text to append to the call's result; empty when there is nothing to say.</returns>
    public string After(ToolCallRecord call)
    {
        ArgumentNullException.ThrowIfNull(call);
        List<string> notes = [.. watchdog.After(call), .. modules.SelectMany(static module => module.TakeNotes())];
        return notes.Count == 0 ? string.Empty : "\n\n" + PromptSections.EditorNote(string.Join(' ', notes));
    }
}
