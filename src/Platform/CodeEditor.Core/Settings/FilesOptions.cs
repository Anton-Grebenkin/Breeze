namespace CodeEditor.Core.Settings;

/// <summary>
/// The <c>files</c> settings section, as in VS Code: <c>"files.exclude": { "**/*.tmp": true }</c>,
/// <c>"files.autoSave": "afterDelay"</c>, <c>"files.autoSaveDelay": 1000</c>.
/// </summary>
public sealed class FilesOptions
{
    public const string Section = "files";

    public const string AutoSaveOff = "off";
    public const string AutoSaveAfterDelay = "afterDelay";
    public const string AutoSaveOnFocusChange = "onFocusChange";

    /// <summary>Pattern to "hide it" flag. Patterns set to <c>false</c> are ignored.</summary>
    public Dictionary<string, bool> Exclude { get; set; } = [];

    /// <summary><c>off</c>, <c>afterDelay</c> or <c>onFocusChange</c>.</summary>
    public string AutoSave { get; set; } = AutoSaveOff;

    /// <summary>Delay after an edit for <c>afterDelay</c>, in ms.</summary>
    public int AutoSaveDelay { get; set; } = 1000;

    public IEnumerable<string> ExcludedPatterns => Exclude.Where(pair => pair.Value).Select(pair => pair.Key);
}
