using System.Windows;
using System.Windows.Controls;
using CodeEditor.Modules.Agent.ViewModels.Chat;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>
/// Message template by kind: the answer as Markdown, a question as a chip, cards and notices in a frame. Agent work
/// (reasoning, tools, progress, checks) is shown by the chain block (<see cref="FeedTemplateSelector"/>). Each message
/// builds only its own elements instead of all of them with visibility toggles.
/// </summary>
public sealed class ChatMessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? User { get; set; }

    public DataTemplate? Assistant { get; set; }

    public DataTemplate? Approval { get; set; }

    public DataTemplate? Error { get; set; }

    public DataTemplate? Notice { get; set; }

    public DataTemplate? Question { get; set; }

    public DataTemplate? Handoff { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        (item as ChatMessageViewModel)?.Kind switch
        {
            ChatMessageKind.User => User,
            ChatMessageKind.Assistant => Assistant,
            ChatMessageKind.Approval => Approval,
            ChatMessageKind.Error => Error,
            ChatMessageKind.Notice => Notice,
            ChatMessageKind.Question => Question,
            ChatMessageKind.Handoff => Handoff,
            _ => null,
        };
}
