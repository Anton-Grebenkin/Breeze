using System.Runtime.InteropServices;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// A process's visible top-level windows via Win32. Searching from the UI Automation desktop queries every process's
/// windows and can hang for a long time on another app's unresponsive window.
/// </summary>
internal static class ProcessWindows
{
    public static IReadOnlyList<nint> VisibleTopLevel(int processId)
    {
        var windows = new List<nint>();
        EnumWindows((handle, _) =>
        {
            if (IsWindowVisible(handle)
                && GetWindowThreadProcessId(handle, out var owner) != 0
                && owner == (uint)processId)
            {
                windows.Add(handle);
            }

            return true;
        }, 0);

        return windows;
    }

    private delegate bool EnumWindowsCallback(nint handle, nint parameter);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint handle);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
}
