using System.Collections.Concurrent;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// The page's console messages and errors (<see cref="BrowserConsoleEvents"/>) until the agent reads them, at most
/// <see cref="Capacity"/>: the oldest go first. Events arrive on the UI thread, the agent reads on its own.
/// </summary>
public sealed class BrowserConsoleLog
{
    public const int Capacity = 200;

    private readonly ConcurrentQueue<BrowserConsoleMessage> _messages = new();

    public void Record(string method, string json)
    {
        if (BrowserConsoleEvents.Parse(method, json) is not { } message)
        {
            return;
        }

        _messages.Enqueue(message);
        while (_messages.Count > Capacity && _messages.TryDequeue(out _))
        {
        }
    }

    /// <summary>Messages since the last call.</summary>
    public IReadOnlyList<BrowserConsoleMessage> Take()
    {
        var messages = new List<BrowserConsoleMessage>();
        while (_messages.TryDequeue(out var message))
        {
            messages.Add(message);
        }

        return messages;
    }
}
