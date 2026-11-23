namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// The browser failed: not created, or the page did not open or load in time. The message is for the model and the user.
/// </summary>
public sealed class BrowserException : Exception
{
    public BrowserException()
    {
    }

    public BrowserException(string message)
        : base(message)
    {
    }

    public BrowserException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The call did not finish because a page of the agent's opened this dialog and waits for the answer: the agent
    /// answers it with the <c>dialog</c> action instead of waiting for a timeout.
    /// </summary>
    public BrowserDialog? Dialog { get; init; }
}
