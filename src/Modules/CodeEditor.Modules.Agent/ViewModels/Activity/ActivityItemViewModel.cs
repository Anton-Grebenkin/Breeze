using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>
/// A step in a work block's chain (ADR 0010): reasoning, a progress line, a tool or an exploration group. The first and
/// last steps know their position so the chain line starts and ends at their icons.
/// </summary>
public abstract partial class ActivityItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsFirst { get; set; }

    [ObservableProperty]
    public partial bool IsLast { get; set; }

    /// <summary>Whether this step shows the given feed message.</summary>
    public abstract bool Contains(ChatMessageViewModel message);

    /// <summary>Unsubscribes from messages when the step leaves the feed.</summary>
    public virtual void Detach()
    {
    }
}
