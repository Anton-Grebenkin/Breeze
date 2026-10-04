using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using CodeEditor.App.Resources;
using CodeEditor.Shell.Instances;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Velopack.Locators;

namespace CodeEditor.App.Startup;

/// <summary>
/// Adds and removes the Explorer items and file types (<see cref="ExplorerIntegration"/>): the installer hooks call it
/// after install and every update and before uninstall, and an installed build repairs the items at startup, since
/// nothing reports a hook that failed. A failure must not break installing or starting, so it is ignored.
/// </summary>
internal static partial class ExplorerRegistration
{
    private const int AssociationsChanged = 0x08000000;

    /// <summary>Writes what is missing or outdated (after a language change, too); an unchanged registry stays as is.</summary>
    /// <returns>Whether anything was written.</returns>
    public static bool Register() => TryEdit(user => ExplorerRegistry.Write(user, Values()));

    public static void Unregister() => TryEdit(user =>
    {
        ExplorerRegistry.Remove(user, Values());
        return true;
    });

    public static async Task RepairAsync(ILogger logger)
    {
        if (IsInstalled() && await Task.Run(Register).ConfigureAwait(false))
        {
            LogRepaired(logger);
        }
    }

    // Portable and development builds don't touch the registry: their location is not permanent.
    private static bool IsInstalled() =>
        VelopackLocator.IsCurrentSet && VelopackLocator.Current is { IsPortable: false, CurrentlyInstalledVersion: not null };

    private static IReadOnlyList<RegistryValue> Values() =>
        ExplorerIntegration.Values(Launcher(), new ExplorerTexts(Strings.OpenInBreeze, Strings.FileTypeName, Strings.AppDescription));

    // The launcher in the installation root stays in place; current\, where this process runs, is replaced by updates.
    private static string Launcher()
    {
        var current = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        return Path.Combine(Path.GetDirectoryName(current)!, Path.GetFileName(Environment.ProcessPath!));
    }

    /// <returns>Whether the registry changed; Explorer is told so it rereads the menus and file types.</returns>
    private static bool TryEdit(Func<RegistryKey, bool> edit)
    {
        try
        {
            if (!edit(Registry.CurrentUser))
            {
                return false;
            }

            SHChangeNotify(AssociationsChanged, 0, 0, 0);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            // Explorer simply won't offer Breeze; the app itself works.
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Explorer items and file types registered")]
    private static partial void LogRepaired(ILogger logger);

    [DllImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
