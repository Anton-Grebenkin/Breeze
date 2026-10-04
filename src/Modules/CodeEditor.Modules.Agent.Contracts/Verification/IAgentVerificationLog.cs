namespace CodeEditor.Modules.Agent.Contracts.Verification;

/// <summary>
/// Results of the agent's code checks, an external "antithesis" to its solution (ADR 0008). The build module reports
/// whether build and tests passed; the agent's verification gate matches this against edits and does not let a turn
/// end with unverified code.
/// </summary>
public interface IAgentVerificationLog
{
    /// <returns>
    /// A hint for the model after repeated consecutive failures (name the cause and change approach), appended to the
    /// tool result; <c>null</c> if no hint is needed.
    /// </returns>
    string? Record(VerificationKind kind, bool succeeded);
}
