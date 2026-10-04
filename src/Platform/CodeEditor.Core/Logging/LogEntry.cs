using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Logging;

/// <summary>Log entry ready for the file: message and exception are formatted on the calling thread.</summary>
public readonly record struct LogEntry(DateTimeOffset Time, LogLevel Level, string Category, string Message, int ThreadId)
{
    /// <summary>Exception with stack trace (<see cref="Exception.ToString"/>), if any.</summary>
    public string? Exception { get; init; }

    /// <summary>Scopes (<c>BeginScope</c>) joined with "→", e.g. the agent chat id.</summary>
    public string? Scopes { get; init; }
}
