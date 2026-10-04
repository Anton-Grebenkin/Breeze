using System.Threading.Channels;
using CodeEditor.Modules.Terminal.Services.Pty;

namespace CodeEditor.Modules.Terminal.Tests.Panel;

/// <summary>A shell without a process: the test writes its output and ends it.</summary>
internal sealed class FakeTerminalProcess(int processId) : ITerminalProcess
{
    private readonly Channel<string> _output = Channel.CreateUnbounded<string>();
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ProcessId { get; } = processId;

    public ChannelReader<string> Output => _output.Reader;

    public Task<int> Exited => _exited.Task;

    public List<string> Input { get; } = [];

    public List<(int Columns, int Rows)> Sizes { get; } = [];

    public bool Disposed { get; private set; }

    public void Print(string text) => _output.Writer.TryWrite(text);

    public void Exit(int code)
    {
        _output.Writer.TryComplete();
        _exited.TrySetResult(code);
    }

    public void Write(string text) => Input.Add(text);

    public void Resize(int columns, int rows) => Sizes.Add((columns, rows));

    public void Dispose()
    {
        Disposed = true;
        Exit(-1);
    }
}
