using CodeEditor.Core.Commands;
using CodeEditor.Shell.Services;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.ViewModels;

/// <summary>A notification in the bar: a button closes it and runs its command.</summary>
public sealed class NotificationViewModel
{
    public NotificationViewModel(Notification notification, ICommandService commands, Action<NotificationViewModel> close)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(close);
        Message = notification.Message;
        AutomationId = $"Notification.{notification.Id}";
        CloseCommand = new RelayCommand(() => close(this));
        Buttons =
        [
            .. notification.Actions.Select(action => new NotificationButtonViewModel(
                action.Title,
                $"{AutomationId}.{action.CommandId}",
                action.IsPrimary,
                new AsyncRelayCommand(async () =>
                {
                    close(this);
                    await commands.ExecuteAsync(action.CommandId).ConfigureAwait(true);
                }))),
        ];
    }

    public string Message { get; }

    public string AutomationId { get; }

    public string CloseAutomationId => AutomationId + ".Close";

    public IReadOnlyList<NotificationButtonViewModel> Buttons { get; }

    public IRelayCommand CloseCommand { get; }
}
