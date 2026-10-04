using CodeEditor.Core.Processes;
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
