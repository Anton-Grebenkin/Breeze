namespace CodeEditor.Modules.Agent.Contracts.Verification;

/// <summary>
/// What the agent can verify code with in the workspace; implemented by the build module. The verification gate
/// requires build and tests only if there is something to run them with, so a folder without a project wastes no steps.
/// </summary>
public interface IAgentVerifier
{
    /// <summary>There is something to build without naming a project (a solution or a single project at the root).</summary>
    bool CanBuild { get; }

    /// <summary>There are unit tests.</summary>
    bool CanTest { get; }
}
