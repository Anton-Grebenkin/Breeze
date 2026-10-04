using CodeEditor.Modules.Agent.Contracts.Verification;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>What the agent can verify with: build (root solution or project) and tests (test projects).</summary>
public sealed class DotNetVerifier(DotNetTarget target, DotNetTests tests) : IAgentVerifier
{
    public bool CanBuild => target.FindDefault() is not null;

    public bool CanTest => tests.HasUnitTests;
}
