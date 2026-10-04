namespace CodeEditor.Modules.Terminal.Services.Pty;

public interface ITerminalProcessFactory
{
    /// <exception cref="System.ComponentModel.Win32Exception">The shell or the pseudo console could not start.</exception>
    ITerminalProcess Start(TerminalLaunch launch);
}
