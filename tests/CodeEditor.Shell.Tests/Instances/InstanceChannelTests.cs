using System.Threading.Channels;
using CodeEditor.Shell.Instances;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Instances;

/// <summary>A launch hands its request to a running window over a real named pipe.</summary>
public sealed class InstanceChannelTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // An id no real process has: the pipe name is derived from it.
    private readonly WindowEntry _window = new(int.MaxValue - Random.Shared.Next(1_000_000), DateTime.UtcNow, @"C:\repo", DateTimeOffset.UtcNow);
    private readonly Channel<InstanceRequest> _received = Channel.CreateUnbounded<InstanceRequest>();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _server;

    public InstanceChannelTests()
    {
        var server = new InstanceServer(NullLogger<InstanceServer>.Instance);
        _server = server.RunAsync(_window.PipeName, request => _received.Writer.WriteAsync(request).AsTask(), _stopping.Token);
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _server.Wait(Timeout);
        _stopping.Dispose();
    }

    [Fact]
    public async Task Requests_AreDeliveredOneAfterAnother()
    {
        Assert.True(await InstanceClient.TrySendAsync(_window, @"C:\repo\a.cs", Timeout));
        Assert.True(await InstanceClient.TrySendAsync(_window, null, Timeout));

        Assert.Equal(new InstanceRequest(@"C:\repo\a.cs"), await _received.Reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new InstanceRequest(null), await _received.Reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WindowWithoutServer_IsReportedQuickly()
    {
        var gone = _window with { ProcessId = _window.ProcessId - 1 };

        Assert.False(await InstanceClient.TrySendAsync(gone, null, TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public async Task Stopping_EndsTheServer()
    {
        await _stopping.CancelAsync();

        await _server.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.True(_server.IsCompletedSuccessfully);
    }
}
