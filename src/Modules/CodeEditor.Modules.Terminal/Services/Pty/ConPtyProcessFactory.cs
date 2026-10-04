namespace CodeEditor.Modules.Terminal.Services.Pty;

public sealed class ConPtyProcessFactory : ITerminalProcessFactory
{
    public ITerminalProcess Start(TerminalLaunch launch) => ConPtyProcess.Start(launch);
}
