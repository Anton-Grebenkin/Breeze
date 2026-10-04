using System.Threading.Channels;

namespace CodeEditor.Modules.Terminal.Services.Pty;

/// <summary>A shell running in a pseudo console: text in, text with VT sequences out, resizable.</summary>
public interface ITerminalProcess : IDisposable
{
    int ProcessId { get; }

    /// <summary>Decoded output in arrival order; completes after the shell has exited and its output is read.</summary>
    ChannelReader<string> Output { get; }

    /// <summary>The exit code, once the shell has exited.</summary>
    Task<int> Exited { get; }

    /// <summary>Keyboard input as the terminal produces it, escape sequences included.</summary>
    void Write(string text);

    void Resize(int columns, int rows);
}
