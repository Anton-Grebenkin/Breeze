namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>
/// Spills long tool output to a file (ADR 0012), as Claude Code and Cursor do: the model gets the head and tail, the
/// size and the path, and reads the full text in parts with <c>read_file</c> or searches it with <c>search_text</c>.
/// One build log does not bloat the context and nothing is lost. Implemented by the agent module.
/// </summary>
public interface IAgentOutputStore
{
    /// <summary>Output for the model: whole if within the limit, otherwise head and tail with a link to the full text file.</summary>
    /// <param name="toolName">Part of the file name, to show whose output it is.</param>
    string Fit(string text, string toolName);
}
