using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// The wrapping agent commands use on this machine (ADR 0052). It starts as <see cref="PowerShellWrapping.Full"/>;
/// when antivirus refuses the wrapped script it becomes <see cref="PowerShellWrapping.Bare"/> and is remembered in the
/// user data folder, so the block happens once rather than on every start.
/// </summary>
public sealed partial class PowerShellMode(IFileSystem fileSystem, UserDataPaths paths, ILogger<PowerShellMode> logger)
{
    public const string FileName = "agent-shell.json";

    private const string BareContent = "{\"wrapping\":\"bare\"}";

    private readonly Lock _lock = new();
    private PowerShellWrapping? _wrapping;

    public PowerShellWrapping Wrapping
    {
        get
        {
            lock (_lock)
            {
                return _wrapping ??= Read();
            }
        }
    }

    /// <summary>The wrapped script was blocked: commands run bare from now on.</summary>
    /// <returns><c>false</c> if they already ran bare, so there is nothing left to fall back to.</returns>
    public bool TryFallBack()
    {
        lock (_lock)
        {
            if ((_wrapping ??= Read()) == PowerShellWrapping.Bare)
            {
                return false;
            }

            _wrapping = PowerShellWrapping.Bare;
            LogFellBack(logger);
            try
            {
                fileSystem.WriteAllBytesAtomic(paths.File(FileName), Encoding.UTF8.GetBytes(BareContent));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogNotSaved(logger, exception.Message);
            }

            return true;
        }
    }

    private PowerShellWrapping Read()
    {
        var path = paths.File(FileName);
        try
        {
            return fileSystem.FileExists(path) && fileSystem.ReadAllText(path).Contains("bare", StringComparison.Ordinal)
                ? PowerShellWrapping.Bare
                : PowerShellWrapping.Full;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogNotRead(logger, exception.Message);
            return PowerShellWrapping.Full;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Antivirus blocked the wrapped agent command: commands run without the wrapper from now on")]
    private static partial void LogFellBack(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent shell mode was not saved: {Error}")]
    private static partial void LogNotSaved(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent shell mode was not read: {Error}")]
    private static partial void LogNotRead(ILogger logger, string error);
}
