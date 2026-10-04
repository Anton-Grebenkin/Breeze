using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Approvals;

/// <summary>
/// An agent question in the feed (<c>ask_user</c>): option buttons and a free-form answer box. The turn awaits
/// <see cref="Answer"/>; stopping the turn withdraws the question.
/// </summary>
public sealed partial class QuestionCardViewModel : ObservableObject
{
    private readonly TaskCompletionSource<string> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public QuestionCardViewModel(string question, IReadOnlyList<string> options)
    {
        Question = question;
        Options = options;
    }

    public string Question { get; }

    public IReadOnlyList<string> Options { get; }

    public Task<string> Answer => _answer.Task;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string CustomAnswer { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending))]
    public partial string? State { get; private set; }

    public bool IsPending => State is null;

    public void Cancel()
    {
        if (_answer.TrySetCanceled())
        {
            State = Strings.QuestionWithdrawn;
        }
    }

    [RelayCommand]
    private void Choose(string? option)
    {
        if (!string.IsNullOrWhiteSpace(option))
        {
            Resolve(option);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private void Submit() => Resolve(CustomAnswer.Trim());

    private bool CanSubmit() => IsPending && !string.IsNullOrWhiteSpace(CustomAnswer);

    private void Resolve(string answer)
    {
        if (_answer.TrySetResult(answer))
        {
            State = string.Format(CultureInfo.CurrentCulture, Strings.QuestionAnswered, answer);
            SubmitCommand.NotifyCanExecuteChanged();
        }
    }
}
