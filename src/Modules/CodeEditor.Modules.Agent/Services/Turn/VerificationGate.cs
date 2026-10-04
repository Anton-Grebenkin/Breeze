using System.Collections.Frozen;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Contracts.Verification;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Verification gate (ADR 0008): a turn with code edits does not end until a build passes after the last edit
/// (in Deep mode, tests too). Events are ordered by a counter: an agent edit (<see cref="AgentFileState.Written"/>) and a
/// check result (<see cref="IAgentVerificationLog"/>) each get a number, and a check counts if its number is greater
/// than the last code edit's. After repeated failures the model is told to name the cause and change approach.
/// </summary>
public sealed class VerificationGate : IAgentVerificationLog, IDisposable
{
    /// <summary>Reminders per turn; after that the turn ends marked "unverified".</summary>
    public const int MaxReminders = 2;

    private static readonly FrozenSet<string> CodeExtensions = FrozenSet.ToFrozenSet(
        [".cs", ".csproj", ".props", ".targets", ".xaml", ".razor", ".cshtml", ".sln", ".slnx", ".fs", ".fsproj", ".vb", ".vbproj"],
        StringComparer.OrdinalIgnoreCase);

    private readonly AgentFileState _fileState;
    private readonly Lock _lock = new();
    private readonly HashSet<string> _codeFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<VerificationKind, int> _passedAt = [];
    private readonly Dictionary<VerificationKind, int> _failedAt = [];
    private readonly Dictionary<VerificationKind, int> _failuresInRow = [];
    private int _sequence;
    private int _lastCodeWrite;

    public VerificationGate(AgentFileState fileState)
    {
        _fileState = fileState;
        _fileState.Written += OnWritten;
    }

    public int Reminders { get; private set; }

    /// <summary>Event number at which the turn started; earlier edits belong to previous turns.</summary>
    public int TurnStart { get; private set; }

    /// <summary>Event number of the last code edit; 0 if none.</summary>
    public int LastCodeWrite
    {
        get
        {
            lock (_lock)
            {
                return _lastCodeWrite;
            }
        }
    }

    public bool HasCodeEditsInTurn => LastCodeWrite > TurnStart;

    /// <summary>Names of code files changed in this turn, for the reminder.</summary>
    public IReadOnlyCollection<string> CodeFilesInTurn
    {
        get
        {
            lock (_lock)
            {
                return [.. _codeFiles];
            }
        }
    }

    public void Begin()
    {
        lock (_lock)
        {
            TurnStart = _sequence;
            Reminders = 0;
            _codeFiles.Clear();
            _failuresInRow.Clear();
        }
    }

    /// <summary>What blocks ending the turn; <c>null</c> if everything is verified or there were no code edits.</summary>
    public VerificationGap? FindGap(bool requireTests)
    {
        lock (_lock)
        {
            if (_lastCodeWrite <= TurnStart)
            {
                return null;
            }

            return GapOf(VerificationKind.Build) ?? (requireTests ? GapOf(VerificationKind.Tests) : null);
        }
    }

    /// <summary>Counts a reminder; <c>false</c> if the turn's reminder limit is reached.</summary>
    public bool TryRemind()
    {
        if (Reminders >= MaxReminders)
        {
            return false;
        }

        Reminders++;
        return true;
    }

    public string? Record(VerificationKind kind, bool succeeded)
    {
        int failures;
        lock (_lock)
        {
            var number = ++_sequence;
            (succeeded ? _passedAt : _failedAt)[kind] = number;
            failures = succeeded ? 0 : _failuresInRow.GetValueOrDefault(kind) + 1;
            _failuresInRow[kind] = failures;
        }

        return CorrectionHints.AfterFailures(kind, failures);
    }

    /// <summary>Consecutive failed checks of this kind in this turn.</summary>
    public int FailuresInRow(VerificationKind kind)
    {
        lock (_lock)
        {
            return _failuresInRow.GetValueOrDefault(kind);
        }
    }

    public void Dispose() => _fileState.Written -= OnWritten;

    private VerificationGap? GapOf(VerificationKind kind)
    {
        if (_passedAt.GetValueOrDefault(kind) > _lastCodeWrite)
        {
            return null;
        }

        return new VerificationGap(kind, Failed: _failedAt.GetValueOrDefault(kind) > _lastCodeWrite);
    }

    private void OnWritten(object? sender, string path)
    {
        if (!CodeExtensions.Contains(Path.GetExtension(path)))
        {
            return;
        }

        lock (_lock)
        {
            _lastCodeWrite = ++_sequence;
            _codeFiles.Add(Path.GetFileName(path));
        }
    }
}
