namespace CodeEditor.Modules.Agent.Contracts.Turn;

/// <summary>A running tool call as its feed row sees it (<see cref="IAgentToolActivity"/>).</summary>
public interface IAgentToolCall
{
    /// <summary>
    /// Shows "Move to background" on the row of a tool that waits for a long process. The token fires when the user
    /// clicks it: the tool stops waiting and returns what it has, the process goes on. Outside a turn it never fires.
    /// </summary>
    CancellationToken OfferBackground();

    /// <summary>Shows a line of output on the row; any thread.</summary>
    void Report(string line);
}
