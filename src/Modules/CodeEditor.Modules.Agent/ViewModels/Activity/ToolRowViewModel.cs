using CodeEditor.Modules.Agent.Contracts.Feed;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>
/// A tool call row in the feed (ADR 0010): icon, title, result and a clickable file. Reads "Reading Order.cs" while
/// running and "Read Order.cs · lines 1–80" when done. A slow call also shows how long it runs, its last output line
/// and, if it waits for a process, "Move to background".
/// </summary>
public sealed partial class ToolRowViewModel(AgentToolView view) : ObservableObject
{
    private Action? _moveToBackground;

    [ObservableProperty]
    public partial AgentToolIcon Icon { get; private set; } = view.Icon;

    [ObservableProperty]
    public partial string Title { get; private set; } = view.Title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial string? Detail { get; private set; } = view.Detail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    public partial string? FilePath { get; private set; } = view.FilePath;

    [ObservableProperty]
    public partial bool IsFailure { get; private set; } = view.IsFailure;

    /// <summary>"1 min 5 s" while a slow call runs; <c>null</c> otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    public partial string? Elapsed { get; private set; }

    /// <summary>The latest output line of a running build, test run or command.</summary>
    [ObservableProperty]
    public partial string? Output { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveToBackgroundCommand))]
    public partial bool CanMoveToBackground { get; private set; }

    /// <summary>A read or search; the feed collapses consecutive ones.</summary>
    public bool IsExploration { get; private set; } = view.IsExploration;

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public bool HasFile => !string.IsNullOrEmpty(FilePath);

    public bool HasProgress => Elapsed is not null;

    public void Update(AgentToolView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Icon = view.Icon;
        Title = view.Title;
        Detail = view.Detail;
        FilePath = view.FilePath;
        IsFailure = view.IsFailure;
        IsExploration = view.IsExploration;
    }

    /// <param name="moveToBackground">Lets the call's process go on in the background; <c>null</c> hides the button.</param>
    public void ShowProgress(string elapsed, string? output, Action? moveToBackground)
    {
        Elapsed = elapsed;
        Output = output;
        _moveToBackground = moveToBackground;
        CanMoveToBackground = moveToBackground is not null;
    }

    public void EndProgress()
    {
        Elapsed = null;
        Output = null;
        _moveToBackground = null;
        CanMoveToBackground = false;
    }

    [RelayCommand(CanExecute = nameof(CanMoveToBackground))]
    private void MoveToBackground()
    {
        _moveToBackground?.Invoke();
        _moveToBackground = null;
        CanMoveToBackground = false;
    }
}
