using System.Text;
using CodeEditor.Modules.Terminal.Services.Pty;

namespace CodeEditor.Modules.Terminal.Tests.Pty;

/// <summary>Real shells in a Windows pseudo console: output, input, exit code, size and closing.</summary>
public sealed class ConPtyProcessTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    private static readonly string Cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Fact]
    public async Task Command_OutputIsDecodedUtf8_AndEndsAfterExit()
    {
        using var process = Start($"\"{Cmd}\" /c echo привет-breeze");

        var output = await ReadAllAsync(process);

        Assert.Contains("привет-breeze", output, StringComparison.Ordinal);
        Assert.Equal(0, await process.Exited.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExitCode_IsReported()
    {
        using var process = Start($"\"{Cmd}\" /c exit 3");

        Assert.Equal(3, await process.Exited.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    // The typed command echoes back as "set /a 6*7"; only the shell's answer contains 42.
    [Fact]
    public async Task Input_ReachesTheShell()
    {
        using var process = Start($"\"{Cmd}\"");

        process.Write("set /a 6*7\r");
        await WaitForAsync(process, "42");
        process.Write("exit\r");

        Assert.Equal(0, await process.Exited.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Size_IsTheConsoleSize()
    {
        using var process = Start($"\"{Cmd}\" /c mode con", columns: 123);

        Assert.Contains("123", await ReadAllAsync(process), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispose_EndsARunningShell()
    {
        var process = Start($"\"{Cmd}\"");
        await WaitForAsync(process, ">");

        process.Dispose();

        await process.Exited.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void MissingProgram_Throws() =>
        Assert.Throws<System.ComponentModel.Win32Exception>(() => Start(@"""C:\no\such\shell.exe"""));

    private static ConPtyProcess Start(string commandLine, int columns = 80) =>
        ConPtyProcess.Start(new TerminalLaunch(commandLine, Path.GetTempPath(), columns, 25));

    private static async Task<string> ReadAllAsync(ITerminalProcess process)
    {
        var text = new StringBuilder();
        using var timeout = new CancellationTokenSource(Timeout);
        await foreach (var chunk in process.Output.ReadAllAsync(timeout.Token))
        {
            text.Append(chunk);
        }

        return text.ToString();
    }

    private static async Task WaitForAsync(ITerminalProcess process, string expected)
    {
        var text = new StringBuilder();
        using var timeout = new CancellationTokenSource(Timeout);
        while (!text.ToString().Contains(expected, StringComparison.Ordinal))
        {
            text.Append(await process.Output.ReadAsync(timeout.Token));
        }
    }
}
