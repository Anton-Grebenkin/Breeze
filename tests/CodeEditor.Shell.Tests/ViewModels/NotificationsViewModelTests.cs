using CodeEditor.Core.Commands;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Tests.ViewModels;

public sealed class NotificationsViewModelTests : IDisposable
{
    private readonly ShellFixture _shell = new();
    private readonly NotificationsViewModel _bar;
    private readonly List<string> _ran = [];

    public NotificationsViewModelTests()
    {
        _bar = new NotificationsViewModel(_shell.CommandService);
        _shell.Commands.Register(new CommandDefinition("test.yes", "Да", (_, _) => Run("test.yes")));
    }

    [Fact]
    public void Show_AddsTheNotificationWithAutomationIds()
    {
        _bar.Show(Question());

        var item = Assert.Single(_bar.Items);
        Assert.Equal("Notification.question", item.AutomationId);
        Assert.Equal("Notification.question.Close", item.CloseAutomationId);
        Assert.Equal(["Notification.question.test.yes"], item.Buttons.Select(button => button.AutomationId));
    }

    [Fact]
    public async Task Button_ClosesAndRunsItsCommand()
    {
        _bar.Show(Question());

        await _bar.Items[0].Buttons[0].Command.ExecuteAsync(null);

        Assert.Empty(_bar.Items);
        Assert.Equal(["test.yes"], _ran);
    }

    [Fact]
    public void Close_RemovesWithoutRunningAnything()
    {
        _bar.Show(Question());

        _bar.Items[0].CloseCommand.Execute(null);

        Assert.Empty(_bar.Items);
        Assert.Empty(_ran);
    }

    [Fact]
    public void Dispose_RemovesOnlyItsNotification()
    {
        var first = _bar.Show(Question());
        _bar.Show(Question("other"));

        first.Dispose();
        first.Dispose();

        Assert.Equal("Notification.other", Assert.Single(_bar.Items).AutomationId);
    }

    public void Dispose() => _shell.Dispose();

    private static Notification Question(string id = "question") =>
        new(id, "Открывать файлы кода в Breeze?", [new NotificationAction("Да", "test.yes")]);

    private ValueTask Run(string id)
    {
        _ran.Add(id);
        return ValueTask.CompletedTask;
    }
}
