using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>
/// An agent work block in the feed (ADR 0010): consecutive reasoning, progress, tool and check steps as a chain. While
/// the turn runs it is expanded with a "Working · 12 s" title; afterwards it collapses to "Worked 42 s · 9 actions".
/// Consecutive reads and searches collapse into an "Explored" group.
/// </summary>
public sealed partial class ActivityBlockViewModel : ObservableObject
{
    private readonly TimeProvider _time;
    private readonly long _started;
    private long _lastActivity;

    /// <param name="live">A block of the running turn: timed and expanded; otherwise a restored feed, collapsed.</param>
    public ActivityBlockViewModel(TimeProvider time, bool live)
    {
        _time = time;
        _started = _lastActivity = time.GetTimestamp();
        IsLive = live;
        IsRunning = live;
        IsExpanded = live;
    }

    public bool IsLive { get; }

    /// <summary>The consecutive feed messages this block shows, in feed order.</summary>
    public List<ChatMessageViewModel> Messages { get; } = [];

    public ObservableCollection<ActivityItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>The live answer prints under the block, so the chain line continues to it from the last step.</summary>
    public bool HasTail
    {
        get;
        set
        {
            field = value;
            MarkEnds();
        }
    }

    public int ToolCount => Messages.Count(message => message.Kind == ChatMessageKind.Tool);

    /// <summary>A step is still running: a tool works or the model is reasoning.</summary>
    public bool HasRunningStep => Messages.Exists(message => message.IsInProgress);

    public string Title => IsRunning
        ? Format(Strings.WorkingFor, DurationText.Format(_time.GetElapsedTime(_started)))
        : Summary();

    public void Add(ChatMessageViewModel message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Messages.Add(message);
        message.PropertyChanged += OnMessageChanged;
        _lastActivity = _time.GetTimestamp();
        var entry = new ActivityEntryViewModel(message);
        switch (message.Kind)
        {
            case ChatMessageKind.Reasoning:
                Append(new ThinkingItemViewModel(message, _time, IsLive));
                break;
            case ChatMessageKind.Tool when entry.IsExploration && Items.LastOrDefault() is ExplorationGroupViewModel group:
                group.Add(entry);
                break;
            case ChatMessageKind.Tool when entry.IsExploration && Items.LastOrDefault() is ActivityEntryViewModel { IsExploration: true } previous:
                Replace(previous, new ExplorationGroupViewModel([previous, entry]));
                break;
            default:
                Append(entry);
                break;
        }

        OnPropertyChanged(nameof(ToolCount));
        OnPropertyChanged(nameof(HasRunningStep));
        OnPropertyChanged(nameof(Title));
    }

    /// <returns><c>false</c> if the message is not in the block.</returns>
    public bool Remove(ChatMessageViewModel message)
    {
        message.PropertyChanged -= OnMessageChanged;
        if (!Messages.Remove(message))
        {
            return false;
        }

        var item = Items.First(candidate => candidate.Contains(message));
        if (item is not ExplorationGroupViewModel group || !group.Remove(message) || group.Entries.Count == 0)
        {
            item.Detach();
            Items.Remove(item);
            MarkEnds();
        }

        OnPropertyChanged(nameof(ToolCount));
        OnPropertyChanged(nameof(HasRunningStep));
        OnPropertyChanged(nameof(Title));
        return true;
    }

    /// <summary>The turn ended: work time counts up to the last step and the block collapses.</summary>
    public void Finish()
    {
        IsRunning = false;
        IsExpanded = false;
    }

    /// <summary>Called every second to refresh the elapsed time in the running block's title.</summary>
    public void Tick()
    {
        if (!IsRunning)
        {
            return;
        }

        OnPropertyChanged(nameof(Title));
        foreach (var thinking in Items.OfType<ThinkingItemViewModel>())
        {
            thinking.Tick();
        }
    }

    public void Detach()
    {
        foreach (var message in Messages)
        {
            message.PropertyChanged -= OnMessageChanged;
        }

        foreach (var item in Items)
        {
            item.Detach();
        }
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    // Work lasts until the last step ends: a tool finishes later than its row appears.
    private void OnMessageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChatMessageViewModel.IsInProgress))
        {
            return;
        }

        if (sender is ChatMessageViewModel { IsInProgress: false })
        {
            _lastActivity = _time.GetTimestamp();
        }

        OnPropertyChanged(nameof(HasRunningStep));
    }

    private string Summary()
    {
        var steps = ToolCount;
        var thinkingOnly = steps == 0 && Items.All(item => item is ThinkingItemViewModel);
        if (!IsLive)
        {
            return thinkingOnly ? Strings.Thoughts : Format(Strings.WorkLog, Actions(steps));
        }

        var duration = DurationText.Format(_time.GetElapsedTime(_started, _lastActivity));
        return thinkingOnly ? Format(Strings.ThoughtFor, duration) : Format(Strings.WorkedFor, duration, Actions(steps));
    }

    private static string Actions(int count) => Plural.Format(count, Strings.ActionForms);

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    private void Append(ActivityItemViewModel item)
    {
        Items.Add(item);
        MarkEnds();
    }

    // Remove and insert instead of replacing in place: on replace WPF keeps the container's old template (tool row)
    // and binds it to the group.
    private void Replace(ActivityItemViewModel previous, ActivityItemViewModel next)
    {
        var index = Items.IndexOf(previous);
        Items.RemoveAt(index);
        Items.Insert(index, next);
        MarkEnds();
    }

    // The chain line runs from the first step's icon to the last one's.
    private void MarkEnds()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].IsFirst = index == 0;
            Items[index].IsLast = index == Items.Count - 1 && !HasTail;
        }
    }
}
