namespace CodeEditor.Modules.Agent.Contracts.Context;

/// <summary>
/// A module's contribution to the <c>&lt;context&gt;</c> block of every agent message: what the model should know
/// without calling tools, like Copilot's "editorContext" (open file and selected lines, last build result). Short facts
/// only, no file contents: the block goes into every message.
/// </summary>
public interface IAgentContextProvider
{
    /// <returns>Block lines; empty if there is nothing to report.</returns>
    ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken);
}
