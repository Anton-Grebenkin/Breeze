using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace CodeEditor.Shell.Instances;

/// <summary>
/// Sends a request to another window over its pipe (<see cref="InstanceServer"/>). Only the same Windows user can
/// connect. The sender lets the window take the foreground: Windows allows it to the process the user just started.
/// </summary>
public static class InstanceClient
{
    /// <summary><c>false</c> if the window didn't answer in time: it has exited or hangs.</summary>
    public static async Task<bool> TrySendAsync(WindowEntry target, string? path, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(target);
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            _ = AllowSetForegroundWindow(target.ProcessId);
            await using var pipe = new NamedPipeClientStream(".", target.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(cancellation.Token).ConfigureAwait(false);
            await InstanceProtocol.WriteRequestAsync(pipe, new InstanceRequest(path), cancellation.Token).ConfigureAwait(false);
            return await InstanceProtocol.ReadAcknowledgementAsync(pipe, cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
