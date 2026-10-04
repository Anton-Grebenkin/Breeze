namespace CodeEditor.Modules.Agent.Contracts.Feed;

/// <summary>
/// A module's contribution to the agent feed: rows for its own tools. The module knows what the arguments mean and what
/// the result looks like, so it builds the row instead of the chat. Registered in DI next to
/// <see cref="IAgentToolProvider"/>.
/// </summary>
public interface IAgentToolPresenter
{
    /// <returns><c>null</c> for another module's tool: the chat asks the next presenter or shows name and arguments.</returns>
    AgentToolView? Present(AgentToolCall call);
}
