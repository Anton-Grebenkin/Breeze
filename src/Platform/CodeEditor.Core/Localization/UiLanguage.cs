using System.Globalization;
using CodeEditor.Core.Settings;

namespace CodeEditor.Core.Localization;

/// <summary>
/// UI language (ADR 0011): setting <c>workbench.language</c> is <c>auto</c> (Windows language), <c>ru</c> or <c>en</c>.
/// Applied at startup before any window exists, since resources are read by <see cref="CultureInfo.CurrentUICulture"/>.
/// A change takes effect after a restart, as in VS Code; the editor offers one right away (<c>workbench.restart</c>).
/// </summary>
public static class UiLanguage
{
    public const string SettingKey = "workbench.language";

    /// <summary>Environment override of the setting, like VS Code's <c>--locale</c>; used by UI tests.</summary>
    public const string OverrideVariable = "CODEEDITOR_UI_LANGUAGE";
    public const string Auto = "auto";
    public const string Russian = "ru";
    public const string English = "en";

    private const string ConfigurationKey = "workbench:language";

    public static IReadOnlyList<string> Choices { get; } = [Auto, Russian, English];

    /// <summary>
    /// Culture for the setting; <c>auto</c> and unknown values mean the system language if supported, else English.
    /// </summary>
    public static CultureInfo Resolve(string? setting, CultureInfo system)
    {
        ArgumentNullException.ThrowIfNull(system);
        var language = string.IsNullOrWhiteSpace(setting) || string.Equals(setting, Auto, StringComparison.OrdinalIgnoreCase)
            ? system.TwoLetterISOLanguageName
            : setting.Trim().ToLowerInvariant();
        return CultureInfo.GetCultureInfo(language == Russian ? Russian : English);
    }

    /// <summary>Setting value from the user's <c>settings.json</c> text; a parse error counts as no setting.</summary>
    public static string? ReadSetting(string settingsJson)
    {
        ArgumentNullException.ThrowIfNull(settingsJson);
        try
        {
            return SettingsJson.Parse(settingsJson).GetValueOrDefault(ConfigurationKey);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Sets the UI culture process-wide: the current thread and all new ones.</summary>
    public static void Apply(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
