using System.Globalization;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>A queued message above the input box: its first line and a button to remove it before it is sent.</summary>
public sealed partial class QueuedMessageViewModel(QueuedMessage message, int index, Action<QueuedMessage> remove)
{
    public QueuedMessage Message { get; } = message;

    public string Text { get; } = message.Text.Split('\n', 2)[0].Trim();

    public string FullText => Message.Text;

    public string AutomationId { get; } = string.Create(CultureInfo.InvariantCulture, $"Agent.Queued.{index}");

    public string RemoveAutomationId { get; } = string.Create(CultureInfo.InvariantCulture, $"Agent.Queued.{index}.Remove");

    [RelayCommand]
    private void Remove() => remove(Message);
}
