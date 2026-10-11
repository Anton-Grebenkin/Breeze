using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Watchdog;

/// <summary>
/// Watches a turn's tool calls for signs of being stuck and tells the model to change tactics with a note on the tool
/// result: the same error on the same target again (an edit whose fragment is ambiguous, retried five times), a run of
/// failed calls, a slow test run, step checkpoints. Each signal speaks once per streak, so notes don't turn into noise.
/// Build and test failures have their own escalation (<see cref="CorrectionHints"/>).
/// </summary>
public sealed partial class TurnWatchdog(TurnBudget budget)
{
    /// <summary>The same error on the same target this many times gets advice; from <see cref="SameErrorStop"/> a stop.</summary>
    public const int SameErrorNote = 2;

    public const int SameErrorStop = 3;

    /// <summary>Failed calls in a row, of any tools, that call for a pause.</summary>
    public const int FailureStreak = 5;

    /// <summary>Steps between checkpoints: a reminder to compare progress with the plan.</summary>
    public const int CheckpointSteps = 40;

    private const int ShapeLength = 100;
    private const string TestsTool = "run_tests";
    private const string CommandTool = "run_command";

    /// <summary>A test run this long keeps the user waiting.</summary>
    public static readonly TimeSpan SlowRun = TimeSpan.FromSeconds(60);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _errors = new(StringComparer.Ordinal);
    private int _failedInRow;
    private bool _slowNoted;
    private int _nextCheckpoint = CheckpointSteps;

    public void Begin()
    {
        lock (_gate)
        {
            _errors.Clear();
            _failedInRow = 0;
            _slowNoted = false;
            _nextCheckpoint = CheckpointSteps;
        }
    }

    /// <returns>Notes for the model; empty when nothing looks wrong.</returns>
    public IReadOnlyList<string> After(ToolCallRecord call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var notes = new List<string>();
        lock (_gate)
        {
            var target = ToolTarget.Of(call.Arguments);
            if (call.Failed)
            {
                AddFailureNotes(call, target, notes);
            }
            else
            {
                _failedInRow = 0;
                Forget(call.Tool, target);
            }

            AddSlowRunNote(call, notes);
            AddCheckpointNote(notes);
        }

        return notes;
    }

    private void AddFailureNotes(ToolCallRecord call, string target, List<string> notes)
    {
        var key = string.Join('|', call.Tool, target, Shape(call.Result));
        var count = _errors[key] = _errors.GetValueOrDefault(key) + 1;
        var shown = target.Length > 0 ? target : call.Tool;
        if (count >= SameErrorStop)
        {
            notes.Add(Format(Strings.WatchdogStuck, call.Tool, shown, count));
        }
        else if (count == SameErrorNote)
        {
            notes.Add(Format(Strings.WatchdogSameError, call.Tool, shown));
        }

        if (++_failedInRow >= FailureStreak)
        {
            notes.Add(Format(Strings.WatchdogFailureStreak, _failedInRow));
            _failedInRow = 0;
        }
    }

    // A success on the target ends its failure streak: the next error there is a new problem.
    private void Forget(string tool, string target)
    {
        var prefix = tool + "|" + target + "|";
        foreach (var key in _errors.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _errors.Remove(key);
        }
    }

    private void AddSlowRunNote(ToolCallRecord call, List<string> notes)
    {
        if (_slowNoted || call.Elapsed < SlowRun || !IsTestRun(call))
        {
            return;
        }

        _slowNoted = true;
        notes.Add(Format(Strings.WatchdogSlowRun, Durations(call.Elapsed)));
    }

    private void AddCheckpointNote(List<string> notes)
    {
        if (budget.Requests < _nextCheckpoint || budget.Limit == int.MaxValue)
        {
            return;
        }

        notes.Add(Format(Strings.WatchdogCheckpoint, budget.Requests, budget.Limit));
        _nextCheckpoint = budget.Requests + CheckpointSteps;
    }

    private static bool IsTestRun(ToolCallRecord call) =>
        call.Tool == TestsTool
        || (call.Tool == CommandTool && call.Arguments.TryGetValue("command", out var command)
            && command?.ToString()?.Contains("dotnet test", StringComparison.OrdinalIgnoreCase) == true);

    // The error without its variable parts (numbers and number lists, quoted text, paths): "occurs 2 times (lines 105,
    // 152)" and "occurs 3 times (lines 1, 2, 3)" are the same error.
    private static string Shape(string? result)
    {
        var text = Variable().Replace(result ?? string.Empty, "#");
        return text.Length <= ShapeLength ? text : text[..ShapeLength];
    }

    private static string Durations(TimeSpan elapsed) =>
        string.Format(CultureInfo.CurrentCulture, Strings.WatchdogSeconds, (int)elapsed.TotalSeconds);

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    [GeneratedRegex(@"«[^»]*»|""[^""]*""|'[^']*'|`[^`]*`|\S*[\\/]\S*|\d+(?:\s*[,–-]\s*\d+)*", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();
}
