using System.Collections.Concurrent;
using CodeEditor.Core.Context;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Shells;
using CodeEditor.Modules.Terminal.ViewModels;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Terminal.Tests.Panel;

/// <summary>The Terminal panel: several shells, one shown at a time, output batched and kept for the view.</summary>
public sealed class TerminalPanelTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly TerminalFixture _fixture = new();
    private readonly FakeTerminalProcessFactory _processes = new();
    private readonly ContextKeyService _context = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly TestOptionsMonitor<TerminalOptions> _options = new(new TerminalOptions());
    private readonly TerminalPanelViewModel _panel;

    public TerminalPanelTests()
    {
        _fixture.FileSystem
            .AddFile(@"C:\Windows\System32\cmd.exe", string.Empty)
            .AddFile(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", string.Empty);
        var profiles = new TerminalProfiles(_fixture.FileSystem, new ShellLocations(@"C:\Windows\System32", @"C:\Program Files", Path: string.Empty, CommandInterpreter: null));
        _panel = new TerminalPanelViewModel(
            profiles, _processes, _options, _fixture.Workspace, _context, new InlineUiDispatcher(), _statusBar, NullLogger<TerminalPanelViewModel>.Instance);
    }

    public void Dispose()
    {
        _panel.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void EnsureStarted_StartsTheDefaultShellInTheFolder_Once()
    {
        _panel.EnsureStarted();
        _panel.EnsureStarted();

        var launch = Assert.Single(_processes.Launches);
        Assert.Equal(TerminalFixture.Root, launch.WorkingDirectory);
        Assert.Contains("powershell.exe", launch.CommandLine, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Windows PowerShell", _panel.Active?.Title);
    }

    [Fact]
    public void Start_PreferredShellAndFolder()
    {
        _options.Set(new TerminalOptions { DefaultProfile = TerminalProfiles.CommandPrompt });

        var session = _panel.Start(folder: @"C:\repo\src");

        Assert.Equal(TerminalProfiles.CommandPrompt, session?.Profile.Id);
        Assert.Equal(@"C:\repo\src", _processes.Launches[0].WorkingDirectory);
    }

    [Fact]
    public async Task Output_ReachesTheViewAndIsKept()
    {
        var session = _panel.Start()!;
        var shown = new ConcurrentQueue<string>();
        session.Output += (_, text) => shown.Enqueue(text);

        _processes.Processes[0].Print("PS C:\\repo> ");
        await WaitUntilAsync(() => !shown.IsEmpty);

        Assert.Equal(["PS C:\\repo> "], shown);
        Assert.Equal("PS C:\\repo> ", session.Replay);
    }

    [Fact]
    public async Task Exit_AddsTheExitLineAfterTheOutput()
    {
        var session = _panel.Start()!;
        _processes.Processes[0].Print("last line");

        _processes.Processes[0].Exit(3);
        await WaitUntilAsync(() => session.IsExited);

        Assert.StartsWith("last line", session.Replay, StringComparison.Ordinal);
        Assert.Contains("Процесс завершён с кодом 3", session.Replay, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_GoesToTheShellUntilItExits()
    {
        var session = _panel.Start()!;

        session.Write("dir\r");
        session.Resize(100, 40);

        Assert.Equal(["dir\r"], _processes.Processes[0].Input);
        Assert.Equal([(100, 40)], _processes.Processes[0].Sizes);
    }

    [Fact]
    public void Kill_EndsTheShell_TheNeighborBecomesActive()
    {
        var first = _panel.Start()!;
        var second = _panel.Start()!;

        _panel.Kill(second);

        Assert.True(_processes.Processes[1].Disposed);
        Assert.Equal([first], _panel.Sessions);
        Assert.Same(first, _panel.Active);
    }

    [Fact]
    public void Clear_EmptiesTheKeptOutput()
    {
        var session = _panel.Start()!;
        var cleared = false;
        session.Cleared += (_, _) => cleared = true;

        _panel.Clear();

        Assert.True(cleared);
        Assert.Empty(session.Replay);
    }

    [Fact]
    public void StartFailure_IsReported()
    {
        _processes.Fails = true;

        Assert.Null(_panel.Start());

        Assert.Empty(_panel.Sessions);
        Assert.StartsWith("Не удалось запустить Windows PowerShell", _statusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Focus_SetsTheContextKey()
    {
        _panel.SetFocused(true);
        Assert.Equal(true, _context.GetValue(TerminalPanelViewModel.FocusContextKey));

        _panel.SetFocused(false);
        Assert.Equal(false, _context.GetValue(TerminalPanelViewModel.FocusContextKey));
    }

    // Long output keeps its tail, cut at a line start so no escape sequence is broken.
    [Fact]
    public async Task Replay_KeepsTheTailFromALineStart()
    {
        var session = _panel.Start()!;
        var line = new string('x', 999) + "\n";
        var lines = TerminalSessionViewModel.ReplayLimit / line.Length + 10;
        var shown = 0;
        session.Output += (_, text) => Interlocked.Add(ref shown, text.Length);

        for (var i = 0; i < lines; i++)
        {
            _processes.Processes[0].Print(line);
        }

        await WaitUntilAsync(() => Volatile.Read(ref shown) == lines * line.Length);
        Assert.True(session.Replay.Length <= TerminalSessionViewModel.ReplayLimit);
        Assert.StartsWith("x", session.Replay, StringComparison.Ordinal);
        Assert.Equal(0, session.Replay.Length % line.Length);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Не дождались условия.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
