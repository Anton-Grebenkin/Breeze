namespace CodeEditor.Modules.Agent.Contracts.Turn;

/// <summary>
/// Checks the agent started that went on in the background and still run, such as a test suite that outlived its
/// wait: a turn shouldn't end with a summary before their results.
/// </summary>
public interface IAgentRunningJobs
{
    /// <summary>One entry per running job for the model, e.g. "#5 `dotnet test`".</summary>
    IReadOnlyList<string> Running();
}
