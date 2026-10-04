using CodeEditor.Core.Logging;
using CodeEditor.Core.Output;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Output.Services;

/// <summary>
/// Writes the application log to the Output panel's log channel (like "Output → Log" in VS Code).
/// The channel is created on first write.
/// </summary>
public sealed class OutputLoggerProvider(IOutputService output) : ILoggerProvider
{
    private readonly Lazy<IOutputChannel> _channel = new(() => output.GetOrCreate(IOutputService.LogChannelName));

    public ILogger CreateLogger(string categoryName) => new OutputLogger(LogCategories.Short(categoryName), _channel);

    public void Dispose()
    {
    }
}
