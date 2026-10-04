using System.Globalization;
using System.Text;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services.Pty;
using CodeEditor.Modules.Terminal.Services.Shells;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Terminal.ViewModels;

/// <summary>
/// One terminal: a shell in a pseudo console. Output reaches the view in one batch per UI turn, however fast the
/// shell writes. The last <see cref="ReplayLimit"/> characters are kept for a view that attaches later or reloads.
/// </summary>
public sealed partial class TerminalSessionViewModel : ObservableObject, IDisposable
{
    public const int ReplayLimit = 512 * 1024;

    // A dim line after the output, as VS Code reports a finished process.
    private const string ExitedFormat = "\r\n\u001b[90m{0}\u001b[0m\r\n";

    private readonly ITerminalProcess _process;
    private readonly IUiDispatcher _ui;
    private readonly Lock _gate = new();
    private readonly StringBuilder _pending = new();
    private readonly StringBuilder _replay = new();
    private bool _flushQueued;

    public TerminalSessionViewModel(int id, TerminalProfile profile, ITerminalProcess process, IUiDispatcher ui)
    {
        Id = id;
        Profile = profile;
        _process = process;
        _ui = ui;
        _ = PumpAsync();
    }

    public int Id { get; }

    public TerminalProfile Profile { get; }

    public string Title => Profile.Name;

    public string AutomationId => $"Terminal.Session.{Id}";

    [ObservableProperty]
    public partial bool IsExited { get; private set; }

    /// <summary>The kept output, for a view that attaches now; <see cref="Output"/> continues it.</summary>
    public string Replay => _replay.ToString();

    /// <summary>New output, on the UI thread.</summary>
    public event EventHandler<string>? Output;

    /// <summary>The screen and the kept output were cleared.</summary>
    public event EventHandler? Cleared;

    public void Write(string text)
    {
        if (!IsExited)
        {
            _process.Write(text);
        }
    }

    public void Resize(int columns, int rows) => _process.Resize(columns, rows);

    public void Clear()
    {
        _replay.Clear();
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _process.Dispose();

    // The output completes after the shell exits, so the exit line comes after the last output.
    private async Task PumpAsync()
    {
        await foreach (var chunk in _process.Output.ReadAllAsync())
        {
            Enqueue(chunk);
        }

        var code = await _process.Exited;
        Enqueue(string.Format(CultureInfo.CurrentCulture, ExitedFormat, string.Format(CultureInfo.CurrentCulture, Strings.TerminalExited, code)));
        _ui.Post(() => IsExited = true);
    }

    private void Enqueue(string text)
    {
        lock (_gate)
        {
            _pending.Append(text);
            if (_flushQueued)
            {
                return;
            }

            _flushQueued = true;
        }

        _ui.Post(Flush);
    }

    private void Flush()
    {
        string text;
        lock (_gate)
        {
            text = _pending.ToString();
            _pending.Clear();
            _flushQueued = false;
        }

        _replay.Append(text);
        TrimReplay();
        Output?.Invoke(this, text);
    }

    // Cut at a line start: a cut through an escape sequence would garble the first replayed line.
    private void TrimReplay()
    {
        if (_replay.Length <= ReplayLimit)
        {
            return;
        }

        var cut = _replay.Length - ReplayLimit;
        while (cut < _replay.Length && _replay[cut - 1] != '\n')
        {
            cut++;
        }

        _replay.Remove(0, cut);
    }
}
