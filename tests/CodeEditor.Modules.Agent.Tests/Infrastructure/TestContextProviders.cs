using CodeEditor.Modules.Agent.Contracts.Context;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>Context provider that records requests and returns preset lines.</summary>
internal sealed class RecordingContextProvider(params string[] lines) : IAgentContextProvider
{
    public List<AgentContextRequest> Requests { get; } = [];

    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return ValueTask.FromResult<IReadOnlyList<string>>(request.IncludeActiveEditor ? lines : []);
    }
}

/// <summary>Broken provider: the message must still be sent, just without its lines.</summary>
internal sealed class FailingContextProvider : IAgentContextProvider
{
    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("сбой модуля");
}
