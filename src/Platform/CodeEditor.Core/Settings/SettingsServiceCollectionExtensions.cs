using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Core.Settings;

public static class SettingsServiceCollectionExtensions
{
    /// <summary>
    /// Exposes a settings section as <c>IOptionsMonitor&lt;T&gt;</c>: <c>"editor.fontSize"</c> maps to the
    /// <c>FontSize</c> property of the <c>editor</c> section class. Values update when settings files change.
    /// </summary>
    public static IServiceCollection AddSettingsSection<T>(this IServiceCollection services, string section)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        services.AddOptions();
        services.AddSingleton<IOptionsChangeTokenSource<T>>(provider =>
            new ConfigurationChangeTokenSource<T>(provider.GetRequiredService<ISettingsService>().Configuration.GetSection(section)));
        services.AddSingleton<IConfigureOptions<T>>(provider => new SettingsSectionBinder<T>(
            provider.GetRequiredService<ISettingsService>().Configuration.GetSection(section),
            provider.GetRequiredService<ILogger<SettingsSectionBinder<T>>>()));
        return services;
    }
}
