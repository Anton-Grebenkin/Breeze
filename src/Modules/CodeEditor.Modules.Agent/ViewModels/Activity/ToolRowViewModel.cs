using CodeEditor.Modules.Agent.Contracts.Feed;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>
/// A tool call row in the feed (ADR 0010): icon, title, result and a clickable file. Reads "Reading Order.cs" while
/// running and "Read Order.cs · lines 1–80" when done.
/// </summary>
public sealed partial class ToolRowViewModel(AgentToolView view) : ObservableObject
{
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

    /// <summary>A read or search; the feed collapses consecutive ones.</summary>
    public bool IsExploration { get; private set; } = view.IsExploration;

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public bool HasFile => !string.IsNullOrEmpty(FilePath);

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
}
