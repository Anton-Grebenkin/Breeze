using System.Globalization;
using System.IO;
using CodeEditor.Core.Localization;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Storage;

namespace CodeEditor.App.Startup;

/// <summary>
/// Applies the UI language before windows and services exist: <c>workbench.language</c> is read straight from the
/// user <c>settings.json</c> because the settings service isn't created yet (ADR 0011).
/// </summary>
internal static class StartupLanguage
{
    public static CultureInfo Apply(UserDataPaths paths)
    {
        var setting = Environment.GetEnvironmentVariable(UiLanguage.OverrideVariable) ?? ReadSetting(paths.File(SettingsService.FileName));
        var culture = UiLanguage.Resolve(setting, CultureInfo.CurrentUICulture);
        UiLanguage.Apply(culture);
        return culture;
    }

    private static string? ReadSetting(string path)
    {
        try
        {
            return File.Exists(path) ? UiLanguage.ReadSetting(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
