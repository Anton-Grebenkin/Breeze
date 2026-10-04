using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>
/// The folder's current chat: opens the latest or a selected one and saves it after every answer: the panel feed, the
/// agent session (so the model remembers the conversation) and token usage.
/// </summary>
public sealed class ChatHistory(ChatHistoryStore store, AgentConversation conversation, TimeProvider time)
{
    public static string NewChatTitle => Strings.NewChat;

    private const int TitleLength = 60;

    private DateTimeOffset _created;

    /// <summary>Saved chat id; <c>null</c> for a new chat without an answer yet.</summary>
    public string? CurrentId { get; private set; }

    public string CurrentTitle { get; private set; } = NewChatTitle;

    /// <summary>Chats of the open folder, latest first.</summary>
    public IReadOnlyList<ChatSummary> List() => store.List();

    /// <summary>The folder's latest chat; <c>null</c> if there were none and a new one started.</summary>
    public ChatTranscript? RestoreLatest()
    {
        StartNew();
        return store.LoadLatest() is { } transcript ? Open(transcript) : null;
    }

    /// <summary>The selected chat; <c>null</c> if its file is gone.</summary>
    public ChatTranscript? Restore(string id) =>
        store.Load(id) is { } transcript ? Open(transcript) : null;

    /// <summary>A new chat is written to a new file on the first answer.</summary>
    public void StartNew()
    {
        CurrentId = null;
        CurrentTitle = NewChatTitle;
        conversation.Reset();
    }

    /// <summary>Deletes the current chat (to the recycle bin) and starts a new one.</summary>
    public void DeleteCurrent()
    {
        if (CurrentId is { } id)
        {
            store.Delete(id);
        }

        StartNew();
    }

    public async Task SaveAsync(IReadOnlyList<ChatMessageViewModel> messages, ContextUsage usage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
        {
            return;
        }

        var now = time.GetLocalNow();
        if (CurrentId is null)
        {
            _created = now;
            CurrentId = ChatHistoryStore.NewId(now);
        }

        var question = messages.FirstOrDefault(static message => message.Kind == ChatMessageKind.User)?.Text;
        CurrentTitle = question is null ? NewChatTitle : Shorten(question);
        store.Save(new ChatTranscript(CurrentId, CurrentTitle, _created, [.. messages.Select(ToTranscript)])
        {
            Updated = now,
            Usage = usage,
            Session = await conversation.SerializeSessionAsync(cancellationToken),
        });
    }

    private ChatTranscript Open(ChatTranscript transcript)
    {
        CurrentId = transcript.Id;
        CurrentTitle = transcript.Title;
        _created = transcript.Created;
        conversation.Restore(transcript.Session);
        return transcript;
    }

    private static ChatTranscriptMessage ToTranscript(ChatMessageViewModel message) =>
        message.Approval is { } card
            ? new ChatTranscriptMessage(ChatMessageKind.Approval, $"{card.Title} — {card.StateText}")
            : new ChatTranscriptMessage(message.Kind, message.Text)
            {
                Attachment = message.Attachment,
                Files = message.Files,
                Tool = message.Tool is { } tool ? new ChatTranscriptTool(tool.Icon, tool.Detail, tool.FilePath, tool.IsFailure, tool.IsExploration) : null,
            };

    // The question's first line, so the chat list title stays on one line.
    private static string Shorten(string text)
    {
        var line = text.AsSpan().Trim();
        var end = line.IndexOfAny('\r', '\n');
        line = end < 0 ? line : line[..end];
        return line.Length <= TitleLength ? line.ToString() : string.Concat(line[..TitleLength], "…");
    }
}
