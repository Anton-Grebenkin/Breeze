using System.Collections.Concurrent;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// Deep mode checkpoints (ADR 0012) and their per-turn limits: a risk review before the first edit, advice on stuck
/// signals, and independent review rounds before the summary. Signals are external only: build and test results,
/// file edits, request budget use. The model's self-assessment barely tells success from failure, so it never
/// triggers the advisor. Does nothing in other modes.
/// </summary>
public sealed class DeepSupervisor : IDisposable
{
    /// <summary>Advisor consultations per turn: the pre-edit risk review and stuck signals.</summary>
    public const int MaxConsultations = 3;

    public const int MaxReviewRounds = 2;

    /// <summary>Edits of the same file that signal editing in circles.</summary>
    public const int SameFileEdits = 3;

    /// <summary>Failed checks in a row that signal a repeating error.</summary>
    public const int FailuresInRow = 2;

    private const int MaxProposalCharacters = 8_000;

    private static readonly string[] EditTools = ["apply_edits", "apply_patch", "create_file"];

    private readonly Advisor _advisor;
    private readonly ChangeCritic _critic;
    private readonly VerificationGate _gate;
    private readonly TurnBudget _budget;
    private readonly AgentFileState _fileState;
    private readonly IWorkspace _workspace;
    private readonly TranscriptRecorder _transcript;
    private readonly ConcurrentQueue<string> _notices = new();
    private readonly ConcurrentDictionary<string, int> _editsPerFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _signals = new(StringComparer.Ordinal);
    private int _consultations;
    private int _reviewRounds;
    private int _reviewedWrite;
    private bool _checkedBeforeEdit;

    public DeepSupervisor(Advisor advisor, ChangeCritic critic, VerificationGate gate, TurnBudget budget, AgentFileState fileState, IWorkspace workspace, TranscriptRecorder transcript)
    {
        _advisor = advisor;
        _critic = critic;
        _gate = gate;
        _budget = budget;
        _fileState = fileState;
        _workspace = workspace;
        _transcript = transcript;
        _fileState.Written += OnWritten;
    }

    public AgentMode Mode { get; private set; }

    public bool IsActive => Mode == AgentMode.Deep;

    public void Begin(AgentMode mode)
    {
        Mode = mode;
        _consultations = 0;
        _reviewRounds = 0;
        _reviewedWrite = _gate.LastCodeWrite;
        _checkedBeforeEdit = false;
        _editsPerFile.Clear();
        _signals.Clear();
    }

    /// <summary>
    /// Before the turn's first edit, the advisor reviews the risks. The edit is not applied: the model gets the advice
    /// and repeats the edit with or without it. <c>null</c> means apply as usual.
    /// </summary>
    public async Task<string?> BeforeEditAsync(string toolName, string proposal, CancellationToken cancellationToken)
    {
        if (!IsActive || _checkedBeforeEdit || !EditTools.Contains(toolName) || !TakeConsultation())
        {
            return null;
        }

        _checkedBeforeEdit = true;
        var shown = proposal.Length <= MaxProposalCharacters ? proposal : string.Concat(proposal.AsSpan(0, MaxProposalCharacters), "…");
        if (await _advisor.ConsultAsync(AdvisorCheckpoint.BeforeFirstEdit, shown, cancellationToken) is not { } advice)
        {
            return null;
        }

        _notices.Enqueue(Format(Strings.AdvisorBeforeEditNotice, advice));
        return Format(Strings.AdvisorBeforeEditResult, advice);
    }

    /// <summary>After a tool: on a new stuck signal, advice to append to the result.</summary>
    public async Task<string?> AfterToolAsync(string toolName, string result, CancellationToken cancellationToken)
    {
        if (!IsActive || NewSignal(toolName) is not { } signal || !TakeConsultation())
        {
            return null;
        }

        if (await _advisor.ConsultAsync(AdvisorCheckpoint.Stuck, signal + "\n\n" + result, cancellationToken) is not { } advice)
        {
            return null;
        }

        _notices.Enqueue(Format(Strings.AdvisorStuckNotice, signal, advice));
        return Format(Strings.AdvisorStuckResult, signal, advice);
    }

    /// <summary>
    /// Independent review before the summary, once build and tests pass. The task is the user's requests in the chat.
    /// A second round runs only after new edits.
    /// </summary>
    /// <param name="report">The agent's summary; the reviewer checks its claims against the diff.</param>
    /// <returns>A turn check, or <c>null</c> if no review is needed or the reviewer did not answer.</returns>
    public async Task<TurnCheck?> ReviewAsync(string report, CancellationToken cancellationToken)
    {
        var lastWrite = _gate.LastCodeWrite;
        if (!IsActive || _reviewRounds >= MaxReviewRounds || lastWrite <= _reviewedWrite)
        {
            return null;
        }

        _reviewRounds++;
        _reviewedWrite = lastWrite;
        var task = TranscriptDigest.UserRequests(_transcript.LastRequest);
        if (await _critic.ReviewAsync(task, report, cancellationToken) is not { } review)
        {
            return null;
        }

        if (review.Passed)
        {
            return new TurnCheck(null, Strings.ReviewPassedStatus);
        }

        var findings = string.Join('\n', [.. review.Blocking.Select(static finding => "- [blocking] " + finding), .. review.Advisory.Select(static finding => "- [advisory] " + finding)]);
        _notices.Enqueue(Format(Strings.ReviewNotice, findings));
        return new TurnCheck(Format(Strings.ReviewFindingsPrompt, findings), Format(Strings.ReviewFindingsStatus, review.Blocking.Count.ToString(CultureInfo.CurrentCulture)));
    }

    /// <summary>Advice and findings to show in the feed as work log lines.</summary>
    public IReadOnlyList<string> TakeNotices()
    {
        var notices = new List<string>();
        while (_notices.TryDequeue(out var notice))
        {
            notices.Add(notice);
        }

        return notices;
    }

    public void Dispose() => _fileState.Written -= OnWritten;

    private bool TakeConsultation() => Interlocked.Increment(ref _consultations) <= MaxConsultations;

    // Each signal fires at most once per turn.
    private string? NewSignal(string toolName)
    {
        var signal = Signal(toolName);
        return signal is not null && _signals.TryAdd(signal, 0) ? signal : null;
    }

    private string? Signal(string toolName)
    {
        if (toolName == "build" && _gate.FailuresInRow(VerificationKind.Build) >= FailuresInRow)
        {
            return Strings.StuckRepeatedBuild;
        }

        if (toolName == "run_tests" && _gate.FailuresInRow(VerificationKind.Tests) >= FailuresInRow)
        {
            return Strings.StuckRepeatedTests;
        }

        if (EditTools.Contains(toolName) && _editsPerFile.FirstOrDefault(pair => pair.Value >= SameFileEdits).Key is { } file)
        {
            return Format(Strings.StuckSameFile, _workspace.RelativePath(file));
        }

        return _budget.Requests * 2 >= _budget.Limit && _gate.FindGap(requireTests: false) is { Failed: true }
            ? Strings.StuckHalfBudget
            : null;
    }

    private void OnWritten(object? sender, string path) => _editsPerFile.AddOrUpdate(path, 1, static (_, count) => count + 1);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
