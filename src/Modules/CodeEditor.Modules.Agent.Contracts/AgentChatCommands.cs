namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>Commands other modules run to reach the agent's chat without referencing the agent module.</summary>
public static class AgentChatCommands
{
    /// <summary>
    /// Shows the chat and moves the caret into its input box; a text argument is added to what is typed there, for the
    /// user to finish the request and send it.
    /// </summary>
    public const string AddToInput = "agent.addToInput";
}
