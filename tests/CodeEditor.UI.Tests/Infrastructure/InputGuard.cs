using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core.Tools;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Input goes only to the app under test. UI tests send real keys and clicks: if the user's browser or editor were on
/// top, <c>Ctrl+W</c> would close its tab. Before a key press the app's window must be in the foreground; before a
/// click, its window must be under the click point. Otherwise the test fails without sending anything.
/// </summary>
internal static class InputGuard
{
    /// <summary>How long to wait for our window to come to the foreground (after a menu closes or focus moves).</summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(2);

    private const uint RootAncestor = 2;

    private static int _processId;

    public static void Attach(int processId) => _processId = processId;

    public static void Detach(int processId)
    {
        if (_processId == processId)
        {
            _processId = 0;
        }
    }

    public static void EnsureKeyboardTarget()
    {
        var target = 0;
        if (!Retry.WhileFalse(() => IsApplication(target = OwnerOf(GetForegroundWindow())), SettleTimeout).Result)
        {
            throw Refused("нажатие клавиш", target);
        }
    }

    public static void EnsurePointerTarget(Point point)
    {
        var target = 0;
        if (!Retry.WhileFalse(() => IsApplication(target = OwnerOf(GetAncestor(WindowFromPoint(new NativePoint(point.X, point.Y)), RootAncestor))), SettleTimeout).Result)
        {
            throw Refused($"щелчок в точке {point.X},{point.Y}", target);
        }
    }

    private static bool IsApplication(int processId) => _processId != 0 && processId == _processId;

    private static int OwnerOf(nint window) => GetWindowThreadProcessId(window, out var owner) == 0 ? 0 : (int)owner;

    private static InvalidOperationException Refused(string action, int owner) =>
        new($"Ввод отменён ({action}): окно другого процесса «{ProcessName(owner)}» поверх приложения. " +
            "Тест остановлен, чтобы не нажимать в чужом окне; не трогайте мышь и клавиатуру во время UI-тестов.");

    private static string ProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return processId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(int X, int Y);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
}
