using CodeEditor.Core.Output;
using CodeEditor.Modules.Output.Services;
using CodeEditor.Modules.Output.ViewModels;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Output.Tests;

public sealed class OutputServiceTests
{
    private readonly OutputService _output = new();

    [Fact]
    public void GetOrCreate_ReturnsSameChannelByName()
    {
        var changes = 0;
        _output.ChannelsChanged += (_, _) => changes++;

        var first = _output.GetOrCreate("Сборка");
        var second = _output.GetOrCreate("Сборка");

        Assert.Same(first, second);
        Assert.Single(_output.Channels);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Logger_WritesInformationAndAbove_ToLogChannel()
    {
        using var provider = new OutputLoggerProvider(_output);
        var logger = provider.CreateLogger("CodeEditor.Core.Commands.CommandService");

        logger.LogDebug("отладка");
        logger.LogInformation("сведения");
        logger.LogError(new InvalidOperationException("сбой"), "ошибка");

        var text = _output.GetOrCreate(IOutputService.LogChannelName).Snapshot();
        Assert.DoesNotContain("отладка", text, StringComparison.Ordinal);
        Assert.Contains("[Инфо] CommandService: сведения", text, StringComparison.Ordinal);
        Assert.Contains("[Ошибка] CommandService: ошибка", text, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException: сбой", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModel_RefreshesOnlyWhenChannelChanged()
    {
        var channel = _output.GetOrCreate("Сборка");
        using var viewModel = new OutputViewModel(_output);

        channel.AppendLine("строка");

        Assert.True(viewModel.Refresh());
        Assert.False(viewModel.Refresh());
        Assert.Equal("строка\n", viewModel.Text);
    }

    [Fact]
    public void ViewModel_PicksFirstChannelWhenItAppears()
    {
        using var viewModel = new OutputViewModel(_output);
        Assert.Null(viewModel.SelectedChannel);

        _output.GetOrCreate("Журнал").AppendLine("старт");
        viewModel.Refresh();

        Assert.Equal("Журнал", viewModel.SelectedChannel?.Name);
        Assert.Equal("старт\n", viewModel.Text);
    }

    [Fact]
    public void ViewModel_Clear_EmptiesSelectedChannel()
    {
        _output.GetOrCreate("Журнал").AppendLine("старт");
        using var viewModel = new OutputViewModel(_output);

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.Text);
    }
}
