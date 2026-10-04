using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Token usage over a response stream. Services report it differently: OpenAI once at the end, ProxyAPI for Claude
/// twice (start and end) as a running total. A repeat with the same input is the same request and replaces the previous
/// value rather than adding to it; a different input is the next request of the tool loop.
/// </summary>
public sealed class UsageAccumulator
{
    private UsageDetails? _pending;

    /// <returns>The previous request's usage if a new request started; otherwise <c>null</c>.</returns>
    public UsageDetails? Add(UsageDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var previous = _pending;
        _pending = details;
        return previous is not null && previous.InputTokenCount == details.InputTokenCount ? null : previous;
    }

    /// <returns>The last request's usage; <c>null</c> if there was none.</returns>
    public UsageDetails? Flush()
    {
        var last = _pending;
        _pending = null;
        return last;
    }
}
