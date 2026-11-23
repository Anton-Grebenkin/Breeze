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

    // The view keeps a cursor and appends what is new: the whole channel is not copied on every change.
    [Fact]
    public void ViewModel_GivesOnlyWhatIsNewSinceTheCursor()
    {
        var channel = _output.GetOrCreate("Сборка");
        using var viewModel = new OutputViewModel(_output);
        channel.AppendLine("строка");

        var first = viewModel.Read(OutputCursor.None);
        var nothing = viewModel.Read(first.Cursor);
        channel.AppendLine("ещё");
        var appended = viewModel.Read(first.Cursor);

        Assert.Equal(("строка\n", true), (first.Text, first.IsReset));
        Assert.True(nothing.IsEmpty);
        Assert.Equal(("ещё\n", false), (appended.Text, appended.IsReset));
    }

    [Fact]
    public void ViewModel_PicksFirstChannelWhenItAppears()
    {
        using var viewModel = new OutputViewModel(_output);
        Assert.Null(viewModel.SelectedChannel);
        Assert.True(viewModel.Read(OutputCursor.None).IsEmpty);

        _output.GetOrCreate("Журнал").AppendLine("старт");

        Assert.Equal("Журнал", viewModel.SelectedChannel?.Name);
        Assert.Equal("старт\n", viewModel.Read(OutputCursor.None).Text);
    }

    // Removed text (a clear, the oldest quarter dropped on overflow) makes the reader start over: what it shows no
    // longer matches the channel.
    [Fact]
    public void ClearAndTrim_ResetTheReader()
    {
        var channel = _output.GetOrCreate("Журнал");
        using var viewModel = new OutputViewModel(_output);
        channel.AppendLine("старт");
        var cursor = viewModel.Read(OutputCursor.None).Cursor;

        viewModel.ClearCommand.Execute(null);
        var cleared = viewModel.Read(cursor);
        channel.AppendLine(new string('x', OutputChannel.MaxLength));
        channel.AppendLine("хвост");
        var trimmed = viewModel.Read(cleared.Cursor);

        Assert.Equal((string.Empty, true), (cleared.Text, cleared.IsReset));
        Assert.True(trimmed.IsReset);
        Assert.Equal(channel.Snapshot(), trimmed.Text);
        Assert.EndsWith("хвост\n", trimmed.Text, StringComparison.Ordinal);
    }

    // A failed build asks for its channel to be shown; the module selects it and brings the panel forward.
    [Fact]
    public void Reveal_RaisesTheRequestForTheChannel()
    {
        var channel = _output.GetOrCreate("Сборка");
        IOutputChannel? requested = null;
        _output.RevealRequested += (_, revealed) => requested = revealed;

        ((IOutputService)_output).Reveal(channel);

        Assert.Same(channel, requested);
    }
}
