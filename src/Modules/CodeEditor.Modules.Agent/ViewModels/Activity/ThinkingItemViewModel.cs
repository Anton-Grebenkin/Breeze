using System.ComponentModel;
using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>
/// Model reasoning in the chain: native reasoning (summary, thinking, reasoning_content) or prompted thinking aloud for
/// models without it (ADR 0010). In a live turn it is expanded and streams under "Thinking…", then reads "Thought for
/// 6 s"; a click collapses it. In a restored chat it starts collapsed so the whole history does not unfold at once.
/// </summary>
public sealed partial class ThinkingItemViewModel : ActivityItemViewModel
{
    private readonly TimeProvider _time;
    private readonly long _started;

    /// <param name="live">Reasoning of the live turn, not a restored chat: timed and expanded.</param>
    public ThinkingItemViewModel(ChatMessageViewModel message, TimeProvider time, bool live)
    {
        Message = message;
        _time = time;
        _started = time.GetTimestamp();
        IsLive = live;
        IsExpanded = live;
        message.PropertyChanged += OnMessageChanged;
    }

    public ChatMessageViewModel Message { get; }

    public bool IsLive { get; }

    public bool IsStreaming => Message.IsInProgress;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial TimeSpan? Duration { get; private set; }

    /// <summary>
    /// After this delay the live title shows elapsed seconds: some models think for minutes behind a short summary, and
    /// without a timer the feed looks frozen.
    /// </summary>
    public static readonly TimeSpan ShowElapsedAfter = TimeSpan.FromSeconds(3);

    public string Title => IsStreaming
        ? StreamingTitle()
        : Duration is { } duration ? string.Format(CultureInfo.CurrentCulture, Strings.ThoughtFor, DurationText.Format(duration)) : Strings.Thoughts;

    /// <summary>Called every second to refresh the elapsed time in the live title.</summary>
    public void Tick()
    {
        if (IsStreaming && IsLive)
        {
            OnPropertyChanged(nameof(Title));
        }
    }

    private string StreamingTitle()
    {
        var elapsed = _time.GetElapsedTime(_started);
        return IsLive && elapsed >= ShowElapsedAfter
            ? string.Format(CultureInfo.CurrentCulture, Strings.ThinkingFor, DurationText.Format(elapsed))
            : Strings.ThinkingNow;
    }

    public override bool Contains(ChatMessageViewModel message) => ReferenceEquals(Message, message);

    public override void Detach() => Message.PropertyChanged -= OnMessageChanged;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    private void OnMessageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChatMessageViewModel.IsInProgress))
        {
            return;
        }

        if (!Message.IsInProgress && IsLive)
        {
            Duration = _time.GetElapsedTime(_started);
        }

        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(Title));
    }
}
