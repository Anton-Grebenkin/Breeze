using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Tests.Commands;

public sealed class CommandServiceTests
{
    private readonly CommandRegistry _registry = new();
    private readonly ContextKeyService _context = new();
    private readonly CollectingLogger<CommandService> _logger = new();
    private readonly CommandService _service;

    public CommandServiceTests() => _service = new CommandService(_registry, _context, _logger);

    [Fact]
    public async Task ExecuteAsync_RunsHandlerWithArgument()
    {
        object? received = null;
        Register("test.run", (argument, _) =>
        {
            received = argument;
            return ValueTask.CompletedTask;
        });

        var status = await _service.ExecuteAsync("test.run", 42, TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.Succeeded, status);
        Assert.Equal(42, received);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_ReturnsNotFound()
    {
        var status = await _service.ExecuteAsync("missing", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.NotFound, status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConditionFalse_ReturnsDisabledAndSkipsHandler()
    {
        var called = false;
        Register("test.run", (_, _) =>
        {
            called = true;
            return ValueTask.CompletedTask;
        }, ContextExpression.Parse("editorFocus"));

        var status = await _service.ExecuteAsync("test.run", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.Disabled, status);
        Assert.False(called);
        Assert.False(_service.CanExecute("test.run"));
    }

    [Fact]
    public void CanExecute_WhenConditionTrue_IsTrue()
    {
        Register("test.run", (_, _) => ValueTask.CompletedTask, ContextExpression.Parse("editorFocus"));
        _context.Set("editorFocus", true);

        Assert.True(_service.CanExecute("test.run"));
    }

    [Fact]
    public async Task ExecuteAsync_HandlerThrows_ReturnsFailedAndLogsError()
    {
        var error = new InvalidOperationException("сбой");
        Register("test.fail", (_, _) => throw error);

        var status = await _service.ExecuteAsync("test.fail", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.Failed, status);
        var entry = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Same(error, entry.Exception);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerAsyncThrows_ReturnsFailed()
    {
        Register("test.fail", async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("сбой");
        });

        var status = await _service.ExecuteAsync("test.fail", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.Failed, status);
    }

    [Fact]
    public async Task ExecuteAsync_Canceled_ReturnsCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        Register("test.long", async (_, token) =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
        });

        var status = await _service.ExecuteAsync("test.long", cancellationToken: cancellation.Token);

        Assert.Equal(CommandExecutionStatus.Canceled, status);
    }

    [Fact]
    public async Task ExecuteAsync_WritesCommandToDebugLog()
    {
        Register("test.run", (_, _) => ValueTask.CompletedTask);

        await _service.ExecuteAsync("test.run", cancellationToken: TestContext.Current.CancellationToken);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.StartsWith("Command test.run executed in", entry.Message, StringComparison.Ordinal);
    }

    private void Register(string id, CommandHandler handler, ContextExpression? when = null) =>
        _registry.Register(new CommandDefinition(id, id, handler, when: when));
}
