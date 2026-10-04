using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Core.Settings;

/// <summary>
/// Binds a section from configuration. A value of the wrong type (<c>"editor.fontSize": "large"</c>) does not crash
/// the app: defaults stay and the error is logged.
/// </summary>
internal sealed partial class SettingsSectionBinder<T>(IConfigurationSection section, ILogger<SettingsSectionBinder<T>> logger) : IConfigureOptions<T>
    where T : class
{
    public void Configure(T options)
    {
        try
        {
            section.Bind(options);
        }
        catch (InvalidOperationException exception)
        {
            LogInvalidValue(logger, section.Path, exception.InnerException?.Message ?? exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid value in settings section '{Section}': {Reason}")]
    private static partial void LogInvalidValue(ILogger logger, string section, string reason);
}
