using System.ComponentModel;
using System.Diagnostics;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;

namespace CodeEditor.UI.Tests.Panels;

/// <summary>
/// Docker panel on real docker (ADR 0033), read-only: opens from the View menu and shows the machine's running
/// containers; without docker or with the engine stopped, a message and a Refresh button. The test triggers no
/// actions, so no container is stopped or removed.
/// </summary>
public sealed class DockerPanelTests(AppSession session) : IClassFixture<AppSession>
{
    private const string ShowDockerCommand = "workbench.view.docker";

    private static readonly TimeSpan DockerTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void ViewMenu_OpensThePanel_WithRunningContainers()
    {
        var running = RunningContainers();

        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem(ShowDockerCommand)).GuardedClick();

        var expected = running is null ? "Docker.Retry" : "Docker.Section.Containers";
        Assert.True(Retry.WhileNull(() => session.TryFind(expected), DockerTimeout).Success, $"Не появился {expected}.");
        if (running is [var first, ..])
        {
            Assert.True(Retry.WhileNull(() => session.TryFind("Docker.Container." + first), DockerTimeout).Success, $"Нет контейнера {first}.");
        }

        session.SaveScreenshot("docker-panel");
    }

    /// <summary>Running container names via the same <c>docker ps</c> the panel uses; <c>null</c> if docker is unavailable.</summary>
    private static List<string>? RunningContainers()
    {
        var start = new ProcessStartInfo("docker", "ps --format {{.Names}}") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)] : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
