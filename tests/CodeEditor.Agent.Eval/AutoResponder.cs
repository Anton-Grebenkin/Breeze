using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CodeEditor.Modules.Agent.ViewModels.Approvals;
using CodeEditor.Modules.Agent.ViewModels.Chat;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Simulated user: approves whatever still asks (commands, deletions; the command policy has already blocked the
/// dangerous ones and the repository is a temp copy), picks the first option of a question or answers "decide
/// yourself". Counts questions, since they cost the user time too.
/// </summary>
internal sealed class AutoResponder : IDisposable
{
    public const string FreeAnswer = "Решай сам, исходя из задачи.";

    private readonly ObservableCollection<ChatMessageViewModel> _messages;

    public AutoResponder(ObservableCollection<ChatMessageViewModel> messages)
    {
        _messages = messages;
        _messages.CollectionChanged += OnMessagesChanged;
    }

    public int Questions { get; private set; }

    public void Dispose() => _messages.CollectionChanged -= OnMessagesChanged;

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var message in e.NewItems?.OfType<ChatMessageViewModel>() ?? [])
        {
            if (message.Approval is { IsPending: true } approval)
            {
                approval.ApproveCommand.Execute(null);
            }
            else if (message.Question is { IsPending: true } question)
            {
                Questions++;
                Answer(question);
            }
        }
    }

    private static void Answer(QuestionCardViewModel question)
    {
        if (question.Options.Count > 0)
        {
            question.ChooseCommand.Execute(question.Options[0]);
        }
        else
        {
            question.CustomAnswer = FreeAnswer;
            question.SubmitCommand.Execute(null);
        }
    }
}
