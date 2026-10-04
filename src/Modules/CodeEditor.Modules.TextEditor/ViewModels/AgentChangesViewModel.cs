using System.Globalization;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.Services.Agent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.TextEditor.ViewModels;

/// <summary>
/// Agent changes in an open file, as in Cursor and Copilot: diff hunks from the chat's original text
/// (<see cref="IAgentFileState.Changes"/>) to the buffer text, each accepted or rejected individually or all at once,
/// with navigation between hunks. Recomputed after agent edits, original-text changes and (debounced) user typing.
/// The view highlights <see cref="Hunks"/>.
/// </summary>
public sealed partial class AgentChangesViewModel : ObservableObject, IDisposable
{
    /// <summary>Recompute debounce after typing, so the diff isn't recomputed per keystroke.</summary>
    public static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(150);

    private readonly IDocument _document;
    private readonly IAgentFileState _fileState;
    private readonly IDocumentService _documents;
    private readonly IAgentChangeReverter _reverter;
    private readonly IUiDispatcher _dispatcher;
    private readonly Action<int> _revealLine;
    private readonly ITimer _typing;

    public AgentChangesViewModel(
        IDocument document,
        IAgentFileState fileState,
        IDocumentService documents,
        IAgentChangeReverter reverter,
        IUiDispatcher dispatcher,
        TimeProvider time,
        Action<int> revealLine)
    {
        ArgumentNullException.ThrowIfNull(time);
        _document = document;
        _fileState = fileState;
        _documents = documents;
        _reverter = reverter;
        _dispatcher = dispatcher;
        _revealLine = revealLine;
        _typing = time.CreateTimer(_ => _dispatcher.Post(Recompute), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _fileState.Written += OnWritten;
        _fileState.ChangesChanged += OnChangesChanged;
        _document.Buffer.Changed += OnBufferChanged;
        Recompute();
    }

    /// <summary>Hunks were recomputed; the view should redraw the highlighting.</summary>
    public event EventHandler? HunksChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(Summary))]
    [NotifyCanExecuteChangedFor(nameof(AcceptAllCommand), nameof(RejectAllCommand), nameof(NextCommand), nameof(PreviousCommand))]
    public partial IReadOnlyList<ChangeHunk> Hunks { get; private set; } = [];

    public bool HasChanges => Hunks.Count > 0;

    /// <summary>E.g. "Agent changes: 3 · +12 −4".</summary>
    public string Summary => string.Format(CultureInfo.CurrentCulture, Strings.AgentChangesSummary, Hunks.Count, Hunks.Sum(hunk => hunk.AddedCount), Hunks.Sum(hunk => hunk.RemovedLines.Count));

    /// <summary>The hunk containing the line, otherwise the nearest one.</summary>
    public ChangeHunk? HunkAt(int line) =>
        Hunks.FirstOrDefault(hunk => hunk.Contains(line)) ?? Hunks.MinBy(hunk => Math.Abs(hunk.NewStart - line));

    public void Dispose()
    {
        _fileState.Written -= OnWritten;
        _fileState.ChangesChanged -= OnChangesChanged;
        _document.Buffer.Changed -= OnBufferChanged;
        _typing.Dispose();
    }

    [RelayCommand]
    private async Task AcceptHunkAsync(ChangeHunk? hunk)
    {
        if (hunk is null || !_fileState.Changes.TryGetValue(_document.FilePath, out var original))
        {
            return;
        }

        var current = _document.Buffer.GetText();
        var accepted = ChangeHunks.Accept(original ?? string.Empty, current, hunk);
        if (accepted == current)
        {
            await AcceptAllAsync();
            return;
        }

        _fileState.UpdateOriginal(_document.FilePath, accepted);
    }

    /// <summary>Restores the hunk's original lines; agent edits are already on disk, so the file is saved again.</summary>
    [RelayCommand]
    private async Task RejectHunkAsync(ChangeHunk? hunk)
    {
        if (hunk is null || !Hunks.Contains(hunk))
        {
            return;
        }

        // Unsaved typing of the user stays unsaved: only a file that matched the disk is written.
        var wasSaved = !_document.IsDirty;
        ChangeHunks.Reject(_document.Buffer, hunk);
        _fileState.RecordRejected(_document.FilePath);
        // Our own edit: recompute now, without the typing debounce.
        Recompute();
        if (wasSaved && !_document.IsDeletedOnDisk)
        {
            await _documents.SaveAsync(_document);
        }
    }

    /// <summary>Accept all: saves the file and removes it from the chat's change list.</summary>
    [RelayCommand(CanExecute = nameof(HasChanges))]
    private async Task AcceptAllAsync()
    {
        _fileState.AcceptFile(_document.FilePath);
        if (_document.IsDirty && !_document.IsDeletedOnDisk)
        {
            await _documents.SaveAsync(_document);
        }
    }

    /// <summary>
    /// Reject all: restores the original text (an agent-created file goes to the recycle bin); the agent re-reads the file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasChanges))]
    private async Task RejectAllAsync()
    {
        if (!_fileState.Changes.TryGetValue(_document.FilePath, out var original))
        {
            return;
        }

        await _reverter.RevertAsync(_document.FilePath, original);
        _fileState.AcceptFile(_document.FilePath);
        _fileState.RecordRejected(_document.FilePath);
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Next(int? fromLine) => Reveal(Hunks.FirstOrDefault(hunk => hunk.NewStart > (fromLine ?? 0)) ?? Hunks[0]);

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Previous(int? fromLine) => Reveal(Hunks.LastOrDefault(hunk => hunk.NewStart < (fromLine ?? int.MaxValue)) ?? Hunks[^1]);

    private void Reveal(ChangeHunk hunk) => _revealLine(Math.Max(1, hunk.IsRemoval ? hunk.NewStart - 1 : hunk.NewStart));

    private void OnWritten(object? sender, string path) => OnChangesChanged(sender, path);

    // Events arrive on the tool's thread; recompute on the UI thread, which owns the buffer.
    private void OnChangesChanged(object? sender, string? path)
    {
        if (path is null || string.Equals(path, _document.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            _dispatcher.Post(Recompute);
        }
    }

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        if (HasChanges || _fileState.Changes.ContainsKey(_document.FilePath))
        {
            _typing.Change(TypingDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Recompute()
    {
        var tracked = _fileState.Changes.TryGetValue(_document.FilePath, out var original);
        var hunks = tracked ? ChangeHunks.Compute(original ?? string.Empty, _document.Buffer.GetText()) : [];
        if (hunks.Count == 0 && Hunks.Count > 0 && tracked)
        {
            // All hunks rejected, the file is back to its original: untrack it so the chat's change list drops it.
            _fileState.AcceptFile(_document.FilePath);
        }

        if (hunks.Count == 0 && Hunks.Count == 0)
        {
            return;
        }

        Hunks = hunks;
        HunksChanged?.Invoke(this, EventArgs.Empty);
    }
}
