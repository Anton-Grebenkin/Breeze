using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Processes;

/// <summary>
/// Windowless process with redirected output, read as UTF-8 with the console code page as a fallback
/// (<see cref="ProcessOutputReader"/>). Input gets the request's text, if any, and is closed, so a command waiting for
/// user input gets end-of-input instead of hanging. Cancellation and timeout kill the whole process tree (builds start
/// MSBuild nodes, tests start hosts).
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(5);

    // A grandchild that outlives the process may keep the output pipes open: don't wait for it forever.
    private static readonly TimeSpan DrainWait = TimeSpan.FromSeconds(2);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var output = new BoundedOutput();
        using var process = new Process { StartInfo = StartInfo(request) };

        var started = Stopwatch.GetTimestamp();
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.ProcessStartFailed, request.FileName, exception.Message), exception);
        }

        // Output is read before input is written: a child that answers while reading would otherwise fill the pipe.
        var reading = Task.WhenAll(
            ProcessOutputReader.ReadLinesAsync(process.StandardOutput.BaseStream, line => Receive(line, output, onLine)),
            ProcessOutputReader.ReadLinesAsync(process.StandardError.BaseStream, line => Receive(line, output, onLine)));
        await WriteInputAsync(process, request.Input);

        var timedOut = false;
        try
        {
            timedOut = await WaitAsync(process, request.Timeout, cancellationToken);
        }
        finally
        {
            await ((Task)reading.WaitAsync(DrainWait, CancellationToken.None)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        return new ProcessResult(timedOut ? -1 : process.ExitCode, output.Text, timedOut, Stopwatch.GetElapsedTime(started));
    }

    // Bytes go to the pipe itself: a writer's buffer left after a broken pipe would throw again when disposed.
    private static async Task WriteInputAsync(Process process, string? input)
    {
        try
        {
            if (input is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(input));
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The process exited without reading its input.
        }
    }

    /// <returns><c>true</c> if the timeout expired and the process was killed.</returns>
    private static async Task<bool> WaitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(KillWait);
        }
        catch (InvalidOperationException)
        {
            // The process has already exited.
        }
    }

    private static void Receive(string line, BoundedOutput output, Action<string>? onLine)
    {
        output.Add(line);
        onLine?.Invoke(line);
    }

    private static ProcessStartInfo StartInfo(ProcessRequest request)
    {
        var info = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in request.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (request.HideSecretVariables)
        {
            foreach (var name in info.Environment.Keys.Where(SecretVariables.IsSecret).ToList())
            {
                info.Environment.Remove(name);
            }
        }

        foreach (var (name, value) in request.Environment)
        {
            info.Environment[name] = value;
        }

        return info;
    }
}
