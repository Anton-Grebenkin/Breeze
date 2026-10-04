namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>A chain step backed by a single message: a tool call, a progress line or a check.</summary>
public sealed class ActivityEntryViewModel(ChatMessageViewModel message) : ActivityItemViewModel
{
    public ChatMessageViewModel Message { get; } = message;

    /// <summary>A read or search step; consecutive ones collapse into an "Explored" group.</summary>
    public bool IsExploration => Message.Tool?.IsExploration ?? false;

    public override bool Contains(ChatMessageViewModel message) => ReferenceEquals(Message, message);
}
