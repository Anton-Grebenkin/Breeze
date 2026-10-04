using System.Globalization;

namespace CodeEditor.Shell.Instances;

/// <summary>
/// What Breeze adds to Windows for the current user (<c>HKCU</c>, no administrator rights): "Open in Breeze" on files,
/// folders and the background of a folder; Breeze in "Open with" for every file and, through a file type of its own
/// per extension (<c>Breeze.cs</c>, as VS Code does), for the files of <see cref="OpenWithFileTypes"/>; and the app in
/// "Default apps", where those types can be given to Breeze. Explorer starts the app with the path, and the launch goes
/// to the window that has it (ADR 0044). In Windows 11 the context menu items are under "Show more options".
/// </summary>
public static class ExplorerIntegration
{
    private const string AppName = "Breeze";
    private const string Classes = @"Software\Classes\";
    private const string Verb = @"shell\Breeze";
    private const string FileVerbKey = Classes + @"*\" + Verb;
    private const string FolderVerbKey = Classes + @"Directory\" + Verb;
    private const string FolderBackgroundVerbKey = Classes + @"Directory\Background\" + Verb;
    private const string ApplicationKey = Classes + @"Applications\Breeze.exe";
    private const string AnyFileOpenWithKey = Classes + @"*\OpenWithList\Breeze.exe";
    private const string AppKey = @"Software\BreezeCodeEditor";
    private const string CapabilitiesKey = AppKey + @"\Capabilities";
    private const string RegisteredApplicationsKey = @"Software\RegisteredApplications";

    /// <summary>
    /// Keys that belong to Breeze, removed with their subkeys on uninstall. Values in keys shared with Windows and other
    /// apps (<c>.cs\OpenWithProgids</c>, <c>RegisteredApplications</c>) are removed one by one.
    /// </summary>
    public static IReadOnlyList<string> OwnedKeys { get; } =
    [
        FileVerbKey,
        FolderVerbKey,
        FolderBackgroundVerbKey,
        ApplicationKey,
        AnyFileOpenWithKey,
        AppKey,
        .. OpenWithFileTypes.Extensions.Select(extension => Classes + ProgId(extension)),
    ];

    public static bool IsOwned(string key) =>
        OwnedKeys.Any(owned => key.Equals(owned, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(owned + @"\", StringComparison.OrdinalIgnoreCase));

    /// <param name="executable">The installation's launcher, which stays in place across updates.</param>
    public static IReadOnlyList<RegistryValue> Values(string executable, ExplorerTexts texts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(texts);
        var openFile = $"\"{executable}\" \"%1\"";

        // %V is the folder for Directory and the current folder for Directory\Background.
        var openFolder = $"\"{executable}\" \"%V\"";
        var icon = $"\"{executable}\",0";
        return
        [
            .. ContextMenuItem(FileVerbKey, texts.OpenIn, icon, openFile),
            .. ContextMenuItem(FolderVerbKey, texts.OpenIn, icon, openFolder),
            .. ContextMenuItem(FolderBackgroundVerbKey, texts.OpenIn, icon, openFolder),
            new(ApplicationKey, "FriendlyAppName", AppName),
            new(ApplicationKey + @"\DefaultIcon", null, icon),
            new(ApplicationKey + @"\shell\open\command", null, openFile),
            new(AnyFileOpenWithKey, null, string.Empty),
            new(CapabilitiesKey, "ApplicationName", AppName),
            new(CapabilitiesKey, "ApplicationDescription", texts.Description),
            new(CapabilitiesKey, "ApplicationIcon", icon),
            new(RegisteredApplicationsKey, AppName, CapabilitiesKey),
            .. OpenWithFileTypes.Extensions.SelectMany(extension => FileType(extension, texts.FileType, icon, openFile)),
        ];
    }

    private static RegistryValue[] ContextMenuItem(string key, string text, string icon, string command) =>
    [
        new(key, null, text),
        new(key, "Icon", icon),
        new(key + @"\command", null, command),
    ];

    private static RegistryValue[] FileType(string extension, string nameFormat, string icon, string command)
    {
        var progId = ProgId(extension);
        var name = string.Format(CultureInfo.CurrentCulture, nameFormat, extension[1..].ToUpperInvariant());
        return
        [
            new(Classes + progId, null, name),
            new(Classes + progId + @"\DefaultIcon", null, icon),
            new(Classes + progId + @"\shell\open\command", null, command),
            new(Classes + extension + @"\OpenWithProgids", progId, string.Empty),
            new(ApplicationKey + @"\SupportedTypes", extension, string.Empty),
            new(CapabilitiesKey + @"\FileAssociations", extension, progId),
        ];
    }

    private static string ProgId(string extension) => AppName + extension;
}
