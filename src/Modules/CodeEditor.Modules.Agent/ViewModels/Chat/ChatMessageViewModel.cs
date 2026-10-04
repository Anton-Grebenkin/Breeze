using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>A chat message. A model answer grows as fragments arrive.</summary>
public sealed partial class ChatMessageViewModel(ChatMessageKind kind, string text = "") : ObservableObject
{
    public ChatMessageKind Kind { get; } = kind;

    /// <summary>For <see cref="ChatMessageKind.Approval"/>: the card with the diff and decision.</summary>
    public ApprovalCardViewModel? Approval { get; init; }

    /// <summary>For <see cref="ChatMessageKind.Tool"/>: icon, result and file; absent in chats before ADR 0010.</summary>
    public ToolRowViewModel? Tool { get; init; }

    /// <summary>For <see cref="ChatMessageKind.Question"/>: the card; a restored feed has only the text.</summary>
    public QuestionCardViewModel? Question { get; init; }

    /// <summary>The open file the agent saw with a user message; shown as a line under it.</summary>
    public string? Attachment { get; init; }

    /// <summary>Files attached to a user message; their names are shown as a line under it.</summary>
    public IReadOnlyList<string>? Files { get; init; }

    public string? FilesText => Files is { Count: > 0 } files ? string.Join(", ", files) : null;

    /// <summary>A message from the turn queue (ADR 0026), shown in the feed like one sent directly.</summary>
    public static ChatMessageViewModel FromUser(QueuedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new(ChatMessageKind.User, message.Text) { Attachment = message.Attachment, Files = message.Files.Count > 0 ? message.Files : null };
    }

    public string Author => Kind switch
    {
        ChatMessageKind.User => Strings.AuthorUser,
        ChatMessageKind.Assistant => Strings.AgentTitle,
        ChatMessageKind.Tool => Strings.AuthorTool,
        ChatMessageKind.Approval => Strings.AuthorApproval,
        ChatMessageKind.Reasoning => Strings.AuthorReasoning,
        ChatMessageKind.Notice => Strings.AuthorEditor,
        ChatMessageKind.Question => Strings.AuthorAgentQuestion,
        ChatMessageKind.Status => Strings.AuthorCheck,
        ChatMessageKind.Review => Strings.AuthorReviewer,
        ChatMessageKind.Progress => Strings.AgentTitle,
        ChatMessageKind.Handoff => Strings.AuthorEditor,
        _ => Strings.AuthorError,
    };

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    /// <summary>The answer is still streaming or the tool is still running.</summary>
    [ObservableProperty]
    public partial bool IsInProgress { get; set; }

    /// <summary>
    /// The live turn's answer right under the work block, shown as the next chain step. When the model moves on to
    /// tools, its text becomes a progress line in the same place, so nothing jumps (set by <see cref="ChatFeed"/>).
    /// </summary>
    [ObservableProperty]
    public partial bool IsChainTail { get; set; }

    /// <summary>
    /// A step above the chain tail (tool or reasoning) is still running and shows the work itself, so no "Thinking…".
    /// </summary>
    [ObservableProperty]
    public partial bool IsChainBusy { get; set; }

    /// <summary>The first line: the header of a collapsed block (reviewer).</summary>
    public string Headline => Text.IndexOf('\n', StringComparison.Ordinal) is var end and >= 0 ? Text[..end] : Text;

    /// <summary>The text after the first line: the body of a collapsed block.</summary>
    public string Details => Text.IndexOf('\n', StringComparison.Ordinal) is var end and >= 0 ? Text[(end + 1)..] : string.Empty;

    public void Append(string fragment) => Text += fragment;
}
