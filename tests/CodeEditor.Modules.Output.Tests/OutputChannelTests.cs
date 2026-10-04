using CodeEditor.Modules.Output.Services;

namespace CodeEditor.Modules.Output.Tests;

public sealed class OutputChannelTests
{
    [Fact]
    public void AppendLine_AddsLineAndBumpsVersion()
    {
        var channel = new OutputChannel("Журнал");

        channel.AppendLine("первая");
        channel.AppendLine("вторая");

        Assert.Equal("первая\nвторая\n", channel.Snapshot());
        Assert.Equal(2, channel.Version);
    }

    [Fact]
    public void Clear_EmptiesAndBumpsVersion()
    {
        var channel = new OutputChannel("Журнал");
        channel.AppendLine("текст");

        channel.Clear();

        Assert.Empty(channel.Snapshot());
        Assert.Equal(2, channel.Version);
    }

    [Fact]
    public void Overflow_TrimsOldestLinesAtLineBoundary()
    {
        var channel = new OutputChannel("Сборка");
        var line = new string('x', 999);

        for (var i = 0; i < 1_200; i++)
        {
            channel.AppendLine($"{i:D4}{line}");
        }

        var text = channel.Snapshot();
        Assert.True(text.Length <= OutputChannel.MaxLength);
        Assert.StartsWith(text[..4], text);
        Assert.Matches(@"^\d{4}x", text);
        Assert.EndsWith($"1199{line}\n", text);
    }

    [Fact]
    public async Task ConcurrentWriters_LoseNothing()
    {
        const int Writers = 8;
        const int LinesPerWriter = 500;
        var channel = new OutputChannel("Журнал");

        await Task.WhenAll(Enumerable.Range(0, Writers).Select(writer => Task.Run(() =>
        {
            for (var i = 0; i < LinesPerWriter; i++)
            {
                channel.AppendLine($"{writer}:{i}");
            }
        }, TestContext.Current.CancellationToken)));

        Assert.Equal(Writers * LinesPerWriter, channel.Snapshot().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(Writers * LinesPerWriter, channel.Version);
    }
}
