using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Commands;

/// <summary>
/// A command that fails tells the user: whoever ran it (a key, a menu item) drops the result, and the status bar line
/// was overwritten by the next message.
/// </summary>
public sealed class FailureNotifierTests : IDisposable
{
    private readonly CommandRegistry _registry = new();
    private readonly CommandService _commands;
    private readonly FakeNotifications _notifications = new();
    private readonly FailureNotifier _notifier;

    public FailureNotifierTests()
    {
        _commands = new CommandService(_registry, new ContextKeyService(), NullLogger<CommandService>.Instance);
        _notifier = new FailureNotifier(_commands, _notifications, new InlineUiDispatcher());
    }

    public void Dispose() => _notifier.Dispose();

    [Fact]
    public async Task FailedCommand_IsShownWithItsTitleAndReason_AndOffersTheLog()
    {
        _registry.Register(new CommandDefinition("file.export", "Экспорт", (_, _) => throw new InvalidOperationException("диск недоступен"), category: "Файл"));
        _registry.Register(new CommandDefinition("file.ok", "Готово", (_, _) => ValueTask.CompletedTask));

        var failed = await _commands.ExecuteAsync("file.export", cancellationToken: TestContext.Current.CancellationToken);
        await _commands.ExecuteAsync("file.ok", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.Failed, failed);
        var shown = Assert.Single(_notifications.Shown);
        Assert.Equal((FailureNotifier.NotificationId, "Команда «Файл: Экспорт» не выполнена: диск недоступен"), (shown.Id, shown.Message));
        Assert.Equal(LogCommands.OpenLogId, Assert.Single(shown.Actions).CommandId);
    }

    // An error the UI thread caught goes the same way.
    [Fact]
    public void OtherFailure_IsShownAsIs()
    {
        _notifier.Show("Не удалось обновить панель: файл занят.");

        Assert.Equal("Не удалось обновить панель: файл занят.", Assert.Single(_notifications.Shown).Message);
    }
}
