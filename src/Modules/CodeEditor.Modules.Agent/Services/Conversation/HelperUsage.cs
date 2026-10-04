namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Token usage of helper requests (context summary, scout, cache warm-up). Accumulated on background threads; the turn
/// takes it after each reply into the chat's total usage. Not counted toward context fill: helper requests have their
/// own history.
/// </summary>
public sealed class HelperUsage
{
    private long _input;
    private long _output;
    private long _cached;

    /// <param name="cachedInputTokens">The part of the input read from cache.</param>
    public void Add(long inputTokens, long outputTokens, long cachedInputTokens = 0)
    {
        Interlocked.Add(ref _input, inputTokens);
        Interlocked.Add(ref _output, outputTokens);
        Interlocked.Add(ref _cached, cachedInputTokens);
    }

    /// <returns>Usage accumulated since the previous call.</returns>
    public HelperUsageTotals Take() => new(Interlocked.Exchange(ref _input, 0), Interlocked.Exchange(ref _output, 0), Interlocked.Exchange(ref _cached, 0));
}
