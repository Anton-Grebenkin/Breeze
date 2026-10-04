using System.ComponentModel;
using CodeEditor.Modules.Terminal.Services.Pty;

namespace CodeEditor.Modules.Terminal.Tests.Panel;

internal sealed class FakeTerminalProcessFactory : ITerminalProcessFactory
{
    public List<TerminalLaunch> Launches { get; } = [];

    public List<FakeTerminalProcess> Processes { get; } = [];

    /// <summary>The next start fails, as with a shell deleted after detection.</summary>
    public bool Fails { get; set; }

    public ITerminalProcess Start(TerminalLaunch launch)
    {
        Launches.Add(launch);
        if (Fails)
        {
            throw new Win32Exception(2, "Не удаётся найти указанный файл");
        }

        var process = new FakeTerminalProcess(1000 + Processes.Count);
        Processes.Add(process);
        return process;
    }
}
