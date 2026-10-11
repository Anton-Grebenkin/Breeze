using System.Collections.Concurrent;
using CodeEditor.Modules.Agent.Contracts.Turn;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>The feed row of every tool call: records output lines; <see cref="MoveToBackground"/> is the user's click.</summary>
internal sealed class FakeToolActivity : IAgentToolActivity, IAgentToolCall, IDisposable
{
    private readonly CancellationTokenSource _background = new();

    public ConcurrentQueue<string> Lines { get; } = new();

    public bool Offered { get; private set; }

    public IAgentToolCall Current() => this;

    public CancellationToken OfferBackground()
    {
        Offered = true;
        return _background.Token;
    }

    public void Report(string line) => Lines.Enqueue(line);

    public void MoveToBackground() => _background.Cancel();

    public void Dispose() => _background.Dispose();
}
