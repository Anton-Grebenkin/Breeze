using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Services.Commands;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>
/// Agent commands in real Windows PowerShell 5.1 (<see cref="PowerShellLauncher"/>): encoding, chains, exit code,
/// environment.
/// </summary>
public sealed class PowerShellLauncherTests
{
    private static readonly string Folder = Path.GetTempPath();

    private readonly ProcessRunner _runner = new();

    // Non-ASCII output intact, && chains, the native exit code and an environment without secrets (ADR 0012).
    [Fact]
    public async Task AgentPowerShell_Utf8_Chains_ExitCode_NoSecrets()
    {
        const string secret = "CODEEDITOR_TEST_API_TOKEN";
        Environment.SetEnvironmentVariable(secret, "sk-hidden");
        try
        {
            var request = PowerShellLauncher.Request($"Write-Output 'Привет' && Write-Output \"[$env:{secret}]\" && cmd /c exit 5", Folder, TimeSpan.FromSeconds(60));

            var result = await _runner.RunAsync(request, null, TestContext.Current.CancellationToken);

            Assert.Equal(5, result.ExitCode);
            Assert.Contains("Привет", result.Output, StringComparison.Ordinal);
            Assert.Contains("[]", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("sk-hidden", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(secret, null);
        }
    }

    // Running text from an environment variable through a script block is what antivirus blocks (ADR 0052).
    [Fact]
    public void Script_IsPlainText_WithoutCodeFromTheEnvironment()
    {
        var request = PowerShellLauncher.Request("git status", Folder, TimeSpan.FromSeconds(60));

        Assert.Contains("\n& {\ngit status\n}\n", request.Arguments[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("ScriptBlock", request.Arguments[^1], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.Environment, variable => variable.Value.Contains("git status", StringComparison.Ordinal));
    }

    [Fact]
    public void Script_TooLongForACommandLine_IsRejected() =>
        Assert.Contains("слишком длинная", Assert.Throws<AgentToolException>(() =>
            PowerShellLauncher.Request("Write-Output '" + new string('x', PowerShellLauncher.MaxScriptLength) + "'", Folder, TimeSpan.FromSeconds(60))).Message, StringComparison.Ordinal);

    // A trailing comment must not swallow the epilogue; a here-string keeps its lines.
    [Fact]
    public async Task AgentPowerShell_TrailingComment_AndHereString_KeepTheExitCode()
    {
        const string command = "$text = @'\nпервая\nвторая\n'@\nWrite-Output $text\ncmd /c exit 7 # код выхода";

        var result = await _runner.RunAsync(PowerShellLauncher.Request(command, Folder, TimeSpan.FromSeconds(60)), null, TestContext.Current.CancellationToken);

        Assert.Equal(7, result.ExitCode);
        Assert.Contains("первая\nвторая\n", result.Output.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    // Without the prologue PowerShell writes in the console code page: the output is still readable.
    [Fact]
    public async Task AgentPowerShell_Bare_RunsTheCommandAlone_AndOutputIsReadable()
    {
        var request = PowerShellLauncher.Request("Write-Output '░▒▓█'; cmd /c exit 3", Folder, TimeSpan.FromSeconds(60), PowerShellWrapping.Bare);

        var result = await _runner.RunAsync(request, null, TestContext.Current.CancellationToken);

        Assert.Equal("Write-Output '░▒▓█'; cmd /c exit 3", request.Arguments[^1]);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("░▒▓█", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AgentPowerShell_ParseError_IsReadable_AndFails()
    {
        var result = await _runner.RunAsync(PowerShellLauncher.Request("Write-Output ('Привет'", Folder, TimeSpan.FromSeconds(60)), null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.True(result.Output.Length > 0);
        Assert.DoesNotContain((char)0xFFFD, result.Output);
        Assert.DoesNotContain(result.Output.Split('\n'), line => line.Trim() == "Привет");
    }
}
