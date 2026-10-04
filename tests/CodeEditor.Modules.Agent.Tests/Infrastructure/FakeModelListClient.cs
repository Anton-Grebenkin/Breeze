using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>Service model list without the network: a preset list or a failure.</summary>
internal sealed class FakeModelListClient : IModelListClient
{
    public IReadOnlyList<string> Models { get; set; } = [];

    public Exception? Failure { get; set; }

    public int Calls { get; private set; }

    public Task<IReadOnlyList<string>> ListAsync(AgentOptions options, CancellationToken cancellationToken)
    {
        Calls++;
        return Failure is null ? Task.FromResult(Models) : Task.FromException<IReadOnlyList<string>>(Failure);
    }
}
