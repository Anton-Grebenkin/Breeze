namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Chat token usage. <see cref="ContextTokens"/> is the conversation's current size at the model: input and output of
/// the last request. Totals cover the whole chat. <see cref="IsEstimated"/>: the service reported no usage, so this is
/// an estimate from the text.
/// </summary>
public sealed record ContextUsage(long ContextTokens, long TotalInputTokens, long TotalOutputTokens)
{
    public static ContextUsage Empty { get; } = new(0, 0, 0);

    /// <summary>Cached input tokens in the last request.</summary>
    public long CachedInputTokens { get; init; }

    /// <summary>Cached input tokens for the whole chat, billed at the cache read price.</summary>
    public long TotalCachedInputTokens { get; init; }

    public int Requests { get; init; }

    public bool IsEstimated { get; init; }
}
