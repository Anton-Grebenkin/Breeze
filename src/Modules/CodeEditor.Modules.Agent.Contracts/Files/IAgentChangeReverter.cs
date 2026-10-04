namespace CodeEditor.Modules.Agent.Contracts.Files;

/// <summary>
/// Reverts an agent edit, like Undo in Copilot's changes panel. Implemented by the editor module: text goes back into
/// the tab as one undo step (the user saves), a file created by the agent goes to the recycle bin, a deleted one is
/// restored.
/// </summary>
public interface IAgentChangeReverter
{
    /// <param name="path">Full file path.</param>
    /// <param name="originalText">Text before the agent's first edit; <c>null</c> if the agent created the file.</param>
    Task RevertAsync(string path, string? originalText);
}
