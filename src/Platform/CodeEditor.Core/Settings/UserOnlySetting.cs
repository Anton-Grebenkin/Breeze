namespace CodeEditor.Core.Settings;

/// <summary>
/// A setting only the user's own <c>settings.json</c> may set, like VS Code's machine-scoped settings: a folder's
/// <c>.breeze/settings.json</c> comes with someone else's repository, and it must not widen what the agent does without
/// asking or send the API key to another address. The folder's value is ignored (<see cref="SettingsService"/>).
/// </summary>
/// <param name="Key">A dotted key or section: <c>terminal.allowedCommands</c>, <c>agent.approvals</c>.</param>
public sealed record UserOnlySetting(string Key)
{
    /// <summary>The configuration key (<c>terminal:allowedCommands</c>) or a key below it.</summary>
    public bool Covers(string configurationKey)
    {
        ArgumentNullException.ThrowIfNull(configurationKey);
        var key = Key.Replace('.', ':');
        return configurationKey.StartsWith(key, StringComparison.OrdinalIgnoreCase)
            && (configurationKey.Length == key.Length || configurationKey[key.Length] == ':');
    }
}
