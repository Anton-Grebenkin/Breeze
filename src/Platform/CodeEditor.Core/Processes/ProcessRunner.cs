using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Processes;

/// <summary>
/// Windowless process with output redirected as UTF-8. Input is closed right away, so a command waiting for user
/// input gets end-of-input instead of hanging. Cancellation and timeout kill the whole process tree (builds start
/// MSBuild nodes, tests start hosts).
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(5);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var output = new BoundedOutput();
        using var process = new Process { StartInfo = StartInfo(request) };
        process.OutputDataReceived += (_, e) => Receive(e.Data, output, onLine);
        process.ErrorDataReceived += (_, e) => Receive(e.Data, output, onLine);

        var started = Stopwatch.GetTimestamp();
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.ProcessStartFailed, request.FileName, exception.Message), exception);
        }

        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timedOut = await WaitAsync(process, request.Timeout, cancellationToken);
        return new ProcessResult(timedOut ? -1 : process.ExitCode, output.Text, timedOut, Stopwatch.GetElapsedTime(started));
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

    private static void Receive(string? line, BoundedOutput output, Action<string>? onLine)
    {
        if (line is null)
        {
            return;
        }

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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
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
