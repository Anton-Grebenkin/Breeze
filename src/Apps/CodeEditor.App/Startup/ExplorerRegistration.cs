using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using CodeEditor.App.Resources;
using CodeEditor.Shell.Integration;
using CodeEditor.Shell.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Velopack.Locators;

namespace CodeEditor.App.Startup;

/// <summary>
/// Keeps the Explorer entries of an installed build in line with the <c>windowsIntegration</c> settings: after the
/// first frame and on every settings change, in the background. A feature that is off is removed, so entries an
/// earlier version added without asking go away (ADR 0046). The uninstall hook removes everything. A registry failure
/// must not break starting or uninstalling: Explorer just won't offer Breeze.
/// </summary>
internal sealed partial class ExplorerRegistration(
    IOptionsMonitor<WindowsIntegrationOptions> options,
    ILogger<ExplorerRegistration> logger) : IWindowsIntegration, IDisposable
{
    private const int AssociationsChanged = 0x08000000;

    private readonly Lock _gate = new();
    private IDisposable? _subscription;

    public bool IsAvailable { get; } = IsInstalled();

    public void Start()
    {
        if (!IsAvailable || _subscription is not null)
        {
            return;
        }

        _subscription = options.OnChange(_ => ApplyInBackground());
        ApplyInBackground();
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>For the uninstall hook, which runs before the host exists.</summary>
    public static void Unregister()
    {
        try
        {
            Edit(user => ExplorerRegistry.Remove(user, ContextMenu()) | ExplorerRegistry.Remove(user, FileTypes()));
        }
        catch (Exception exception) when (IsRegistryFailure(exception))
        {
            // Uninstalling goes on; Explorer keeps items that point to a missing file.
        }
    }

    // Any settings write raises OnChange for every section; with nothing to change the registry is only read. The
    // current value is read under the lock, so a late run can't restore an older state.
    private void ApplyInBackground() => _ = Task.Run(() =>
    {
        lock (_gate)
        {
            var current = options.CurrentValue;
            var fileTypes = current.FileTypes == true;
            try
            {
                if (Edit(user => Sync(user, ContextMenu(), current.ContextMenu) | Sync(user, FileTypes(), fileTypes)))
                {
                    LogApplied(logger, current.ContextMenu, fileTypes);
                }
            }
            catch (Exception exception) when (IsRegistryFailure(exception))
            {
                LogFailed(logger, exception.Message);
            }
        }
    });

    private static bool Sync(RegistryKey user, RegistrySet set, bool on) =>
        on ? ExplorerRegistry.Write(user, set) : ExplorerRegistry.Remove(user, set);

    /// <returns>Whether the registry changed; then Explorer is told to reread menus, file types and icons.</returns>
    private static bool Edit(Func<RegistryKey, bool> edit)
    {
        if (!edit(Registry.CurrentUser))
        {
            return false;
        }

        SHChangeNotify(AssociationsChanged, 0, 0, 0);
        return true;
    }

    private static RegistrySet ContextMenu() => ExplorerIntegration.ContextMenu(Launcher(), Strings.OpenInBreeze);

    private static RegistrySet FileTypes() =>
        ExplorerIntegration.FileTypes(Launcher(), Strings.FileTypeName, Strings.AppDescription);

    // Portable and development builds have no permanent path to register.
    private static bool IsInstalled() =>
        VelopackLocator.IsCurrentSet && VelopackLocator.Current is { IsPortable: false, CurrentlyInstalledVersion: not null };

    // The launcher in the installation root stays in place; current\, where this process runs, is replaced by updates.
    private static string Launcher()
    {
        var current = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        return Path.Combine(Path.GetDirectoryName(current)!, Path.GetFileName(Environment.ProcessPath!));
    }

    private static bool IsRegistryFailure(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException or IOException;

    [LoggerMessage(Level = LogLevel.Information, Message = "Explorer integration updated: context menu {ContextMenu}, file types {FileTypes}")]
    private static partial void LogApplied(ILogger logger, bool contextMenu, bool fileTypes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Explorer integration not updated: {Reason}")]
    private static partial void LogFailed(ILogger logger, string reason);

    [DllImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
