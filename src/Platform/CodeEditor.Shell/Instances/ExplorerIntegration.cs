namespace CodeEditor.Shell.Instances;

/// <summary>
/// What Breeze adds to Windows Explorer for the current user (<c>HKCU\Software\Classes</c>, no administrator rights):
/// "Open in Breeze" on files, folders and the background of a folder, and Breeze in "Open with" for every file type.
/// Explorer starts the app with the path, and the launch goes to the window that has it (ADR 0044). In Windows 11 the
/// items are under "Show more options".
/// </summary>
public static class ExplorerIntegration
{
    private const string VerbKey = @"shell\Breeze";
    private const string ApplicationKey = @"Applications\Breeze.exe";

    /// <summary>Keys under <c>Software\Classes</c>, each removed with its subkeys on uninstall.</summary>
    public static IReadOnlyList<string> Keys { get; } =
    [
        @"*\" + VerbKey,
        @"Directory\" + VerbKey,
        @"Directory\Background\" + VerbKey,
        ApplicationKey,
        @"*\OpenWithList\Breeze.exe",
    ];

    /// <param name="executable">The installation's launcher, which stays in place across updates.</param>
    /// <param name="verb">The menu item text, e.g. "Open in Breeze".</param>
    public static IReadOnlyList<RegistryValue> Values(string executable, string verb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(verb);
        var openFile = $"\"{executable}\" \"%1\"";

        // %V is the folder for Directory and the current folder for Directory\Background.
        var openFolder = $"\"{executable}\" \"%V\"";
        var icon = $"\"{executable}\",0";
        return
        [
            .. Verb(@"*\" + VerbKey, verb, icon, openFile),
            .. Verb(@"Directory\" + VerbKey, verb, icon, openFolder),
            .. Verb(@"Directory\Background\" + VerbKey, verb, icon, openFolder),
            new(ApplicationKey, "FriendlyAppName", "Breeze"),
            new(ApplicationKey + @"\DefaultIcon", null, icon),
            new(ApplicationKey + @"\shell\open\command", null, openFile),
            new(@"*\OpenWithList\Breeze.exe", null, string.Empty),
        ];
    }

    private static RegistryValue[] Verb(string key, string text, string icon, string command) =>
    [
        new(key, null, text),
        new(key, "Icon", icon),
        new(key + @"\command", null, command),
    ];
}
