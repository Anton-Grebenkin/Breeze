using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;
using static CodeEditor.Modules.Terminal.Services.Pty.ConPtyNative;

namespace CodeEditor.Modules.Terminal.Services.Pty;

/// <summary>
/// A shell in a Windows pseudo console. Output is read on a dedicated thread and decoded from UTF-8 across chunk
/// boundaries. The console keeps its output pipe open after the shell exits, so the exit closes the console, which
/// ends the output; closing it also ends a running shell (a killed terminal).
/// </summary>
public sealed class ConPtyProcess : ITerminalProcess
{
    private const int ReadBufferSize = 16 * 1024;
    private const short MinSize = 1;

    private readonly nint _console;
    private readonly SafeProcessHandle _process;
    private readonly FileStream _input;
    private readonly FileStream _output;
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private int _consoleClosed;

    private ConPtyProcess(nint console, SafeProcessHandle process, int processId, SafeFileHandle input, SafeFileHandle output)
    {
        _console = console;
        _process = process;
        ProcessId = processId;
        _input = new FileStream(input, FileAccess.Write, bufferSize: 0);
        _output = new FileStream(output, FileAccess.Read, bufferSize: 0);
        _ = Task.Factory.StartNew(ReadOutput, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Exited = WaitForExitAsync();
    }

    public int ProcessId { get; }

    public ChannelReader<string> Output => _channel.Reader;

    public Task<int> Exited { get; }

    /// <exception cref="Win32Exception">The pipes, the console or the process could not be created.</exception>
    public static ConPtyProcess Start(TerminalLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);

        // This process writes to inputWrite and reads outputRead; the console uses the other two ends.
        if (!CreatePipe(out var inputRead, out var inputWrite, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        if (!CreatePipe(out var outputRead, out var outputWrite, 0, 0))
        {
            var error = Marshal.GetLastPInvokeError();
            inputRead.Dispose();
            inputWrite.Dispose();
            throw new Win32Exception(error);
        }

        using (inputRead)
        using (outputWrite)
        {
            var result = CreatePseudoConsole(Size(launch.Columns, launch.Rows), inputRead, outputWrite, 0, out var console);
            if (result != 0)
            {
                inputWrite.Dispose();
                outputRead.Dispose();
                throw new Win32Exception(result);
            }

            try
            {
                var process = StartProcess(console, launch);
                _ = CloseHandle(process.Thread);
                return new ConPtyProcess(console, new SafeProcessHandle(process.Process, ownsHandle: true), process.ProcessId, inputWrite, outputRead);
            }
            catch
            {
                ClosePseudoConsole(console);
                inputWrite.Dispose();
                outputRead.Dispose();
                throw;
            }
        }
    }

    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            _input.Write(Encoding.UTF8.GetBytes(text));
            _input.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The shell has exited: there is nobody to read the input.
        }
    }

    public void Resize(int columns, int rows)
    {
        if (Volatile.Read(ref _consoleClosed) == 0)
        {
            _ = ResizePseudoConsole(_console, Size(columns, rows));
        }
    }

    // The process handle stays open until the exit is observed: the exit code is read from it.
    public void Dispose()
    {
        CloseConsole();
        _input.Dispose();
    }

    private static ProcessInformation StartProcess(nint console, TerminalLaunch launch)
    {
        nint size = 0;
        _ = InitializeProcThreadAttributeList(0, 1, 0, ref size);
        var attributes = Marshal.AllocHGlobal(size);
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            try
            {
                // The attribute value is the console handle itself, not a pointer to it.
                if (!UpdateProcThreadAttribute(attributes, 0, PseudoConsoleAttribute, console, nint.Size, 0, 0))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                }

                var startup = new StartupInfoEx { AttributeList = attributes };
                startup.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();

                // Empty standard handles: otherwise a parent with redirected output (a test host, a CI runner) passes
                // its own to the shell, and the output bypasses the console.
                startup.StartupInfo.Flags = UseStdHandles;
                var commandLine = (launch.CommandLine + '\0').ToCharArray();
                return CreateProcess(null, commandLine, 0, 0, false, ExtendedStartupInfoPresent, 0, launch.WorkingDirectory, ref startup, out var process)
                    ? process
                    : throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            finally
            {
                DeleteProcThreadAttributeList(attributes);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(attributes);
        }
    }

    private static Coord Size(int columns, int rows) =>
        new((short)Math.Clamp(columns, MinSize, short.MaxValue), (short)Math.Clamp(rows, MinSize, short.MaxValue));

    private void ReadOutput()
    {
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[ReadBufferSize];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(ReadBufferSize)];
        try
        {
            int read;
            while ((read = _output.Read(bytes)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
                if (count > 0)
                {
                    _channel.Writer.TryWrite(new string(chars, 0, count));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The console closed its end of the pipe.
        }
        finally
        {
            _output.Dispose();
            _channel.Writer.TryComplete();
        }
    }

    private Task<int> WaitForExitAsync()
    {
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new ProcessWaitHandle(_process);
        RegisteredWaitHandle? registration = null;
        registration = ThreadPool.RegisterWaitForSingleObject(handle, (_, _) =>
        {
            registration?.Unregister(null);
            handle.Dispose();
            exited.TrySetResult(GetExitCodeProcess(_process, out var code) ? code : -1);
            _process.Dispose();
            CloseConsole();
        }, null, Timeout.Infinite, executeOnlyOnce: true);
        return exited.Task;
    }

    // ClosePseudoConsole waits until the console's last output is read, so it runs off the caller's thread.
    private void CloseConsole()
    {
        if (Interlocked.Exchange(ref _consoleClosed, 1) == 0)
        {
            _ = Task.Run(() => ClosePseudoConsole(_console));
        }
    }

    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle process) =>
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
    }
}
