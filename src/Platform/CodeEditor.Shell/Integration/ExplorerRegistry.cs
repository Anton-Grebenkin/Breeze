using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CodeEditor.Shell.Integration;

/// <summary>
/// Writes and removes a <see cref="RegistrySet"/> under a registry key. Both touch only what differs, so a repeated
/// call changes nothing and Explorer need not be told.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ExplorerRegistry
{
    /// <returns>Whether anything was written.</returns>
    public static bool Write(RegistryKey root, RegistrySet set)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(set);
        var changed = false;
        foreach (var group in set.Values.GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
        {
            using var key = root.CreateSubKey(group.Key);
            foreach (var value in group.Where(value => key.GetValue(value.Name) as string != value.Data))
            {
                key.SetValue(value.Name ?? string.Empty, value.Data);
                changed = true;
            }
        }

        return changed;
    }

    /// <returns>Whether anything was there to remove.</returns>
    public static bool Remove(RegistryKey root, RegistrySet set)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(set);
        var changed = false;
        foreach (var owned in set.OwnedKeys.Where(owned => Exists(root, owned)))
        {
            root.DeleteSubKeyTree(owned, throwOnMissingSubKey: false);
            changed = true;
        }

        // A shared key's default value is never Breeze's: removing it would break the type for other apps.
        foreach (var value in set.SharedValues)
        {
            using var key = root.OpenSubKey(value.Key, writable: true);
            if (value.Name is not { } name || key?.GetValue(name) is null)
            {
                continue;
            }

            key.DeleteValue(name);
            changed = true;
        }

        return changed;
    }

    private static bool Exists(RegistryKey root, string key)
    {
        using var opened = root.OpenSubKey(key);
        return opened is not null;
    }
}
