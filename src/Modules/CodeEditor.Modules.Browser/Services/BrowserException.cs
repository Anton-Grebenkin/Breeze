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
}
