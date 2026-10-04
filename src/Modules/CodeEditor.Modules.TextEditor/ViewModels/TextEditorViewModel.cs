using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Documents;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Shell.Editors;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.TextEditor.ViewModels;

/// <summary>
/// A document's text editor: caret and selection for the status bar, language, font, and agent changes in the file
/// (<see cref="AgentChanges"/>). The text lives in <see cref="IDocument.Buffer"/>; the ViewModel knows no AvalonEdit types.
/// </summary>
public sealed partial class TextEditorViewModel(IDocument document, EditorSettings settings, EditorFocus focus) : ObservableObject, IDisposable, IPendingReview
{
    private PendingNavigation? _pending;
    private readonly AgentChangesViewModel? _agentChanges;

    /// <summary>Navigation or focus was requested; the view takes it via <see cref="TakeNavigation"/>.</summary>
    public event EventHandler? NavigationRequested;

    public IDocument Document { get; } = document;

    public EditorSettings Settings { get; } = settings;

    /// <summary>Agent changes to accept or reject; <c>null</c> for an editor without an agent (tests).</summary>
    public AgentChangesViewModel? AgentChanges
    {
        get => _agentChanges;
        init
        {
            _agentChanges = value;
            if (value is not null)
            {
                value.PropertyChanged += OnAgentChangesChanged;
            }
        }
    }

    /// <summary>The file has agent edits awaiting review: its tab is highlighted.</summary>
    public bool HasPendingReview => AgentChanges?.HasChanges == true;

    public void Dispose()
    {
        if (AgentChanges is { } changes)
        {
            changes.PropertyChanged -= OnAgentChangesChanged;
            changes.Dispose();
        }
    }

    public string Language { get; } = LanguageNames.ForFile(document.FilePath);

    /// <summary>1-based caret line.</summary>
    [ObservableProperty]
    public partial int CaretLine { get; set; } = 1;

    /// <summary>1-based caret column.</summary>
    [ObservableProperty]
    public partial int CaretColumn { get; set; } = 1;

    [ObservableProperty]
    public partial int SelectionLength { get; set; }

    /// <summary>Selection start offset, for the agent's <c>get_selection</c> tool.</summary>
    public int SelectionStart { get; set; }

    /// <summary>First and last selected lines (1-based) for the agent context; <c>null</c> without a selection.</summary>
    public (int First, int Last)? SelectionLines { get; set; }

    /// <summary>E.g. "Ln 12, Col 5" or "… (42 selected)".</summary>
    public string PositionText => SelectionLength > 0
        ? string.Format(CultureInfo.CurrentCulture, Strings.PositionWithSelection, CaretLine, CaretColumn, SelectionLength)
        : string.Format(CultureInfo.CurrentCulture, Strings.Position, CaretLine, CaretColumn);

    /// <summary>
    /// Moves the caret to a 1-based line and focuses the text. The request is kept: the view of a tab just opened
    /// from <c>Ctrl+P</c> may not exist yet.
    /// </summary>
    public void GoToLine(int line) => Reveal(new EditorLocation(line));

    /// <summary>Shows the location and selects <see cref="EditorLocation.Length"/> chars; the request is kept.</summary>
    public void Reveal(EditorLocation location)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(location.Line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(location.Column, 1);
        _pending = new PendingNavigation(location, !location.PreserveFocus);
        NavigationRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Focuses the text without moving the caret; the request is kept, as with <see cref="Reveal"/>.</summary>
    public void RequestFocus()
    {
        _pending = new PendingNavigation(_pending?.Location, Focus: true);
        NavigationRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Takes the pending request; <c>null</c> if there is none.</summary>
    public PendingNavigation? TakeNavigation()
    {
        var pending = _pending;
        _pending = null;
        return pending;
    }

    /// <summary>Called by the view when text focus changes.</summary>
    public void SetFocused(bool isFocused) => focus.SetFocused(isFocused);

    partial void OnCaretLineChanged(int value) => OnPropertyChanged(nameof(PositionText));

    partial void OnCaretColumnChanged(int value) => OnPropertyChanged(nameof(PositionText));

    partial void OnSelectionLengthChanged(int value) => OnPropertyChanged(nameof(PositionText));

    private void OnAgentChangesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentChangesViewModel.HasChanges))
        {
            OnPropertyChanged(nameof(HasPendingReview));
        }
    }
}
