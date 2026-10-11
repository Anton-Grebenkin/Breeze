namespace CodeEditor.Modules.Agent.Contracts.Turn;

/// <summary>
/// The link between a running tool and its feed row: a long tool (a command, a build, tests) shows its last output
/// line there and learns when the user moves it to the background.
/// </summary>
public interface IAgentToolActivity
{
    /// <summary>
    /// The call being executed. Take it at the start of the tool method: output callbacks run on other threads,
    /// where the call is no longer known.
    /// </summary>
    IAgentToolCall Current();
}
