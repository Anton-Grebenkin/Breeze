using System.Collections.Concurrent;
using CodeEditor.Modules.Agent.Contracts;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Turn budget and rules: the mode (which tools may run), how many model requests were made and which tool calls
/// repeated. On the last allowed request the model gets tools disabled and a wrap-up request, so the turn ends with a
/// report rather than a cutoff. An identical call (same tool and arguments) is marked the second time and blocked after
/// that, against the loops typical of ReAct agents.
/// </summary>
public sealed class TurnBudget
{
    /// <summary>The identical call count from which a repeat is not run.</summary>
    public const int BlockedRepeat = 3;

    /// <summary>
    /// Reads and searches in a row without edits, builds or commands that signal the model is stuck exploring
    /// (ADR 0016). Counted per call: a batch of parallel reads counts in full.
    /// </summary>
    public const int ReadsBeforeReflection = 20;

    private readonly ConcurrentDictionary<string, int> _calls = new(StringComparer.Ordinal);
    private int _requests;
    private int _readsInRow;
    private int _externalContent;

    public int Limit { get; private set; } = int.MaxValue;

    public int Requests => Volatile.Read(ref _requests);

    /// <summary>
    /// The turn called a tool with external content (<see cref="ExternalContent"/>): no memory is extracted from it.
    /// </summary>
    public bool SawExternalContent => Volatile.Read(ref _externalContent) == 1;

    /// <summary>The turn's mode: what may be called (<see cref="ToolPolicy"/>).</summary>
    public AgentMode Mode { get; private set; }

    /// <summary>Ask for an interim summary after a long run of reads; off for the explorer, whose job is reading.</summary>
    public bool ReflectsOnExploration { get; private set; } = true;

    /// <summary>The limit is reached: the last request was only for the summary.</summary>
    public bool IsExhausted => Requests >= Limit;

    /// <param name="limit">Model requests per turn; the explorer has its own, smaller limit.</param>
    /// <param name="reflectsOnExploration">Ask for an interim summary after <see cref="ReadsBeforeReflection"/> reads in a row.</param>
    public void Begin(AgentMode mode = AgentMode.Agent, int limit = AgentModes.RequestLimit, bool reflectsOnExploration = true)
    {
        Mode = mode;
        Limit = limit;
        ReflectsOnExploration = reflectsOnExploration;
        Interlocked.Exchange(ref _requests, 0);
        Interlocked.Exchange(ref _readsInRow, 0);
        Interlocked.Exchange(ref _externalContent, 0);
        _calls.Clear();
    }

    /// <summary>Counts a model request.</summary>
    /// <returns><c>false</c> if this is the last allowed request: summary only, no tools.</returns>
    public bool TryStartRequest() => Interlocked.Increment(ref _requests) < Limit;

    /// <summary>Counts a tool call.</summary>
    /// <returns>How many times this identical call has been made in the turn.</returns>
    public int RegisterCall(string signature) => _calls.AddOrUpdate(signature, 1, static (_, count) => count + 1);

    /// <summary>The model received external text: a page, search results, a browser snapshot.</summary>
    public void RegisterExternalContent() => Interlocked.Exchange(ref _externalContent, 1);

    /// <summary>The workspace changed: earlier calls are no longer repeats.</summary>
    public void ForgetCalls() => _calls.Clear();

    /// <summary>Counts a read or search.</summary>
    /// <returns><c>true</c> after <see cref="ReadsBeforeReflection"/> in a row: time to stop and summarize.</returns>
    public bool RegisterRead()
    {
        if (Interlocked.Increment(ref _readsInRow) < ReadsBeforeReflection)
        {
            return false;
        }

        Interlocked.Exchange(ref _readsInRow, 0);
        return true;
    }

    /// <summary>An edit, build or command means progress: the read streak restarts.</summary>
    public void RegisterProgress() => Interlocked.Exchange(ref _readsInRow, 0);
}
