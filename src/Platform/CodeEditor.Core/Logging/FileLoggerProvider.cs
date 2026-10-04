using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Logging;

/// <summary>
/// File logger provider (<see cref="LogFileWriter"/>). The level comes from <see cref="LogLevelSwitch"/> (setting
/// <c>log.level</c>); scopes (<c>BeginScope</c>) are appended to the line. The app owns the file, not the provider:
/// one file serves both the pre-container startup log and the host log.
/// </summary>
public sealed class FileLoggerProvider(LogFileWriter writer, LogLevelSwitch levels, TimeProvider time) : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) => new FileLogger(LogCategories.Short(categoryName), this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private bool IsEnabled(LogLevel level) => levels.IsEnabled(level);

    private void Write(LogLevel level, string category, string message, Exception? exception) =>
        writer.Write(new LogEntry(time.GetLocalNow(), level, category, message, Environment.CurrentManagedThreadId)
        {
            Exception = exception?.ToString(),
            Scopes = CurrentScopes(),
        });

    private string? CurrentScopes()
    {
        string? result = null;
        _scopes.ForEachScope((scope, _) => result = result is null ? scope?.ToString() : $"{result} → {scope}", (object?)null);
        return result;
    }

    private sealed class FileLogger(string category, FileLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => owner._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => owner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                owner.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
