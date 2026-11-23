namespace CodeEditor.Core.Settings;

/// <summary>Which settings file may set a section, like VS Code's setting scopes.</summary>
public enum SettingScope
{
    /// <summary>
    /// Only the user's own <c>settings.json</c>: the setting reaches outside the folder (the registry, updates, what
    /// runs without asking), so a folder's file, which comes with someone else's repository, must not set it.
    /// </summary>
    User,

    /// <summary>A folder's <c>.breeze/settings.json</c> may set it too: indentation, exclusions, the model.</summary>
    Folder,
}
