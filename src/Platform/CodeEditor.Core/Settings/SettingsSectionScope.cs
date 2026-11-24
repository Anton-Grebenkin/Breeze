namespace CodeEditor.Core.Settings;

/// <summary>
/// The scope a settings section was registered with. A folder's file is read by allow-list: only keys of
/// <see cref="SettingScope.Folder"/> sections pass, minus <see cref="UserOnlySetting"/> keys inside them; anything else,
/// including a section nobody registered, is ignored (<see cref="SettingsService"/>).
/// </summary>
/// <param name="Section">The section name: <c>editor</c>, <c>agent</c>.</param>
public sealed record SettingsSectionScope(string Section, SettingScope Scope)
{
    /// <summary>The configuration key (<c>editor:tabSize</c>) belongs to the section.</summary>
    public bool Covers(string configurationKey)
    {
        ArgumentNullException.ThrowIfNull(configurationKey);
        return configurationKey.StartsWith(Section, StringComparison.OrdinalIgnoreCase)
            && (configurationKey.Length == Section.Length || configurationKey[Section.Length] == ':');
    }
}
