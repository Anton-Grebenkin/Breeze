using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CodeEditor.Shell.Instances;

/// <summary>Writes and removes the values of <see cref="ExplorerIntegration"/> under a registry key.</summary>
[SupportedOSPlatform("windows")]
public static class ExplorerRegistry
{
    /// <summary>Writes only the values that differ, so a repeated call changes nothing.</summary>
    /// <returns>Whether anything was written.</returns>
    public static bool Write(RegistryKey root, IReadOnlyList<RegistryValue> values)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(values);
        var changed = false;
        foreach (var group in values.GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
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

    /// <summary>Deletes Breeze's own keys whole and its values from keys it shares with Windows and other apps.</summary>
    public static void Remove(RegistryKey root, IReadOnlyList<RegistryValue> values)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(values);
        foreach (var key in ExplorerIntegration.OwnedKeys)
        {
            root.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }

        foreach (var value in values.Where(value => value.Name is not null && !ExplorerIntegration.IsOwned(value.Key)))
        {
            using var key = root.OpenSubKey(value.Key, writable: true);
            key?.DeleteValue(value.Name!, throwOnMissingValue: false);
        }
    }
}
