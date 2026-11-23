namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Where the model client says that a request waits to be repeated (<see cref="StreamFailureChatClient"/>); the chat
/// feed shows the countdown, so a pause of half a minute does not look like a hang.
/// </summary>
public sealed class RequestRetries
{
    /// <summary>Raised on the request's thread.</summary>
    public event EventHandler<RequestRetry>? Waiting;

    public void Report(RequestRetry retry) => Waiting?.Invoke(this, retry);
}
