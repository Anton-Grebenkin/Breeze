using CodeEditor.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CodeEditor.App.Diagnostics;

/// <summary>
/// Logging created before the host: the file, the level switch and a factory for the module loader (which needs a
/// logger before the container). The host writes to the same file.
/// </summary>
internal sealed record AppLogging(LogFileWriter File, LogLevelSwitch Levels, ILoggerFactory Bootstrap);
