using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using CodeEditor.App.Resources;
using CodeEditor.Shell.Instances;
using Microsoft.Win32;

namespace CodeEditor.App.Startup;

/// <summary>
/// Adds and removes the Explorer items (<see cref="ExplorerIntegration"/>): called by the installer hooks after install
/// and every update, and before uninstall. A failure must not break installing, so it is ignored.
/// </summary>
internal static class ExplorerRegistration
{
    private const string ClassesKey = @"Software\Classes";
    private const int AssociationsChanged = 0x08000000;

    public static void Register()
    {
        TryEdit(classes =>
        {
            foreach (var value in ExplorerIntegration.Values(Launcher(), Strings.OpenInBreeze))
            {
                using var key = classes.CreateSubKey(value.Key);
                key.SetValue(value.Name ?? string.Empty, value.Data);
            }
        });
    }

    public static void Unregister() =>
        TryEdit(classes => ExplorerIntegration.Keys.ToList().ForEach(key => classes.DeleteSubKeyTree(key, throwOnMissingSubKey: false)));

    // The launcher in the installation root stays in place; current\, where this process runs, is replaced by updates.
    private static string Launcher()
    {
        var current = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        return Path.Combine(Path.GetDirectoryName(current)!, Path.GetFileName(Environment.ProcessPath!));
    }

    private static void TryEdit(Action<RegistryKey> edit)
    {
        try
        {
            using var classes = Registry.CurrentUser.CreateSubKey(ClassesKey);
            edit(classes);
            SHChangeNotify(AssociationsChanged, 0, 0, 0);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            // Explorer simply won't offer Breeze; the app itself works.
        }
    }

    [DllImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
