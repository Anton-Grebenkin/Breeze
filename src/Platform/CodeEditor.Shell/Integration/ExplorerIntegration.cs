using System.Globalization;

namespace CodeEditor.Shell.Integration;

/// <summary>
/// What Breeze adds to Windows for the current user (<c>HKCU</c>, no administrator rights), as two features the user
/// switches separately (<see cref="Settings.WindowsIntegrationOptions"/>, ADR 0046). Explorer starts the app with the
/// path, and the launch goes to the window that has it (ADR 0044).
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
    /// "Open in Breeze" on files, folders and the background of a folder. In Windows 11 it is under "Show more
    /// options"; it changes nothing else.
    /// </summary>
    /// <param name="executable">The installation's launcher, which stays in place across updates.</param>
    /// <param name="openIn">The menu item text, e.g. "Open in Breeze".</param>
    public static RegistrySet ContextMenu(string executable, string openIn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(openIn);
        var icon = Icon(executable);

        // %V is the folder for Directory and the current folder for Directory\Background.
        var openFolder = Command(executable, "%V");
        return new RegistrySet(
            [FileVerbKey, FolderVerbKey, FolderBackgroundVerbKey],
            [
                .. MenuItem(FileVerbKey, openIn, icon, Command(executable, "%1")),
                .. MenuItem(FolderVerbKey, openIn, icon, openFolder),
                .. MenuItem(FolderBackgroundVerbKey, openIn, icon, openFolder),
            ]);
    }

    /// <summary>
    /// Breeze as an app for the files of <see cref="OpenWithFileTypes"/>: "Open with", "Default apps", and a file type
    /// of its own per extension (<c>Breeze.cs</c>, as VS Code does). Windows shows that type's icon for files that have
    /// no app of their own, so this is never registered without the user's consent.
    /// </summary>
    /// <param name="typeNameFormat">The name of a file type with the extension as <c>{0}</c>, e.g. "{0} File".</param>
    /// <param name="description">The app's description in "Default apps".</param>
    public static RegistrySet FileTypes(string executable, string typeNameFormat, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeNameFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var icon = Icon(executable);
        var openFile = Command(executable, "%1");
        return new RegistrySet(
            [
                ApplicationKey,
                AnyFileOpenWithKey,
                AppKey,
                .. OpenWithFileTypes.Extensions.Select(extension => Classes + ProgId(extension)),
            ],
            [
                new(ApplicationKey, "FriendlyAppName", AppName),
                new(ApplicationKey + @"\DefaultIcon", null, icon),
                new(ApplicationKey + @"\shell\open\command", null, openFile),
                new(AnyFileOpenWithKey, null, string.Empty),
                new(CapabilitiesKey, "ApplicationName", AppName),
                new(CapabilitiesKey, "ApplicationDescription", description),
                new(CapabilitiesKey, "ApplicationIcon", icon),
                new(RegisteredApplicationsKey, AppName, CapabilitiesKey),
                .. OpenWithFileTypes.Extensions.SelectMany(extension => FileType(extension, typeNameFormat, icon, openFile)),
            ]);
    }

    private static RegistryValue[] MenuItem(string key, string text, string icon, string command) =>
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

    private static string Command(string executable, string argument) => $"\"{executable}\" \"{argument}\"";

    private static string Icon(string executable) => $"\"{executable}\",0";
}
