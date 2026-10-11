using System.Collections.Concurrent;
using CodeEditor.Modules.Agent.Contracts.Turn;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Tool calls in progress by call id (<see cref="IAgentToolActivity"/>): <see cref="GuardedToolFunction"/> begins and
/// ends each call, a long tool reports output lines and offers to go to the background, and the feed polls
/// <see cref="Snapshot"/> to show it. Only the last line is kept: a build prints thousands, the row shows one.
/// </summary>
public sealed class RunningToolCalls(TimeProvider time) : IAgentToolActivity
{
    private readonly ConcurrentDictionary<string, Call> _calls = new(StringComparer.Ordinal);

    public IAgentToolCall Current() =>
        FunctionInvokingChatClient.CurrentContext?.CallContent.CallId is { } id && _calls.TryGetValue(id, out var call) ? call : IdleCall.Instance;

    /// <summary>Registers the running call; disposing the result ends it.</summary>
    public IDisposable Begin(string callId)
    {
        ArgumentNullException.ThrowIfNull(callId);
        var call = new Call(this, callId, time.GetUtcNow());
        _calls[callId] = call;
        return call;
    }

    public IReadOnlyList<ToolCallState> Snapshot() =>
        [.. _calls.Values.Select(call => new ToolCallState(call.Id, time.GetUtcNow() - call.Started, call.LastLine, call.OffersBackground))];

    /// <returns><c>false</c> if the call has finished or doesn't wait for a process.</returns>
    public bool MoveToBackground(string callId) => _calls.TryGetValue(callId, out var call) && call.RequestBackground();

    /// <returns>How many calls went to the background.</returns>
    public int MoveAllToBackground() => _calls.Values.Count(static call => call.RequestBackground());

    private sealed class Call(RunningToolCalls owner, string id, DateTimeOffset started) : IAgentToolCall, IDisposable
    {
        private readonly CancellationTokenSource _background = new();
        private volatile string? _lastLine;
        private volatile bool _offersBackground;

        public string Id { get; } = id;

        public DateTimeOffset Started { get; } = started;

        public string? LastLine => _lastLine;

        // Once the user asked, the button is gone even before the tool returns.
        public bool OffersBackground => _offersBackground && !_background.IsCancellationRequested;

        public CancellationToken OfferBackground()
        {
            _offersBackground = true;
            return _background.Token;
        }

        // Blank lines would blank the row between the lines that say something.
        public void Report(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _lastLine = line;
            }
        }

        public bool RequestBackground()
        {
            if (!OffersBackground)
            {
                return false;
            }

            try
            {
                _background.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            owner._calls.TryRemove(new KeyValuePair<string, Call>(Id, this));
            _background.Dispose();
        }
    }

    private sealed class IdleCall : IAgentToolCall
    {
        public static readonly IdleCall Instance = new();

        public CancellationToken OfferBackground() => CancellationToken.None;

        public void Report(string line)
        {
        }
    }
}
