using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Token usage over a response stream. Services report it differently: OpenAI once at the end, ProxyAPI for Claude
/// twice (start and end) as a running total. A repeat with the same input is the same request and replaces the previous
/// value rather than adding to it; a different input is the next request of the tool loop. Each report is counted at
/// once, so the context ring keeps up with the tool loop instead of lagging a request behind.
/// </summary>
public sealed class UsageAccumulator
{
    private long? _input;

    /// <returns><c>true</c> if the report revises the current request; <c>false</c> if it starts the next one.</returns>
    public bool Revises(UsageDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var revises = _input is { } input && input == details.InputTokenCount;
        _input = details.InputTokenCount;
        return revises;
    }
}
