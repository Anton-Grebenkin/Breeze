namespace CodeEditor.Core.Storage;

/// <summary>
/// User data folder: layout, settings, log and keys. Defaults to <c>%LOCALAPPDATA%\Breeze</c>; the
/// <c>CODEEDITOR_USER_DATA</c> variable overrides it so UI tests start from a clean slate (like VS Code's
/// <c>--user-data-dir</c>).
/// </summary>
public sealed class UserDataPaths
{
    public const string OverrideVariable = "CODEEDITOR_USER_DATA";

    private const string ProductFolder = "Breeze";

    // Folder name before the product rename (ADR 0038).
    private const string LegacyProductFolder = "CodeEditor";

    public UserDataPaths()
        : this(Environment.GetEnvironmentVariable(OverrideVariable))
    {
    }

    public UserDataPaths(string? root) =>
        Root = string.IsNullOrWhiteSpace(root)
            ? DefaultRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            : root;

    public string Root { get; }

    public string File(string name) => Path.Combine(Root, name);

    /// <summary>
    /// Default folder inside <paramref name="localAppData"/>. Moves the old <c>CodeEditor</c> folder to the new one so
    /// settings, keys and the log survive; if a running old version holds it, keeps using the old folder.
    /// </summary>
    public static string DefaultRoot(string localAppData)
    {
        var current = Path.Combine(localAppData, ProductFolder);
        var legacy = Path.Combine(localAppData, LegacyProductFolder);
        if (Directory.Exists(current) || !Directory.Exists(legacy))
        {
            return current;
        }

        try
        {
            Directory.Move(legacy, current);
            return current;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return legacy;
        }
    }
}
