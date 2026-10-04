using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;

namespace CodeEditor.Core.Tests.Keybindings;

/// <summary>A focused terminal takes keys for the shell; only the commands it keeps resolve.</summary>
public sealed class KeyCaptureTests
{
    private const string TerminalFocus = "terminalFocus";

    private readonly KeybindingRegistry _registry = new();
    private readonly ContextKeyService _context = new();
    private readonly KeyCaptures _captures = new();
    private readonly KeybindingResolver _resolver;

    public KeyCaptureTests()
    {
        _resolver = new KeybindingResolver(_registry, _captures);
        Bind("Ctrl+W", "editor.close");
        Bind("Ctrl+K Ctrl+O", "workbench.folder.open");
        Bind("Ctrl+Shift+P", "workbench.showCommands");
        _captures.Register(new KeyCapture(TerminalFocus, command => command == "workbench.showCommands"));
    }

    [Fact]
    public void NoFocus_KeysGoTheUsualWay()
    {
        Assert.Equal("editor.close", Resolve("Ctrl+W").Binding?.CommandId);
        Assert.Equal(KeyResolutionKind.WaitingForSecondChord, Resolve("Ctrl+K").Kind);
    }

    [Fact]
    public void Focused_OtherKeysReachTheElement_ChordsDoNotStart()
    {
        _context.Set(TerminalFocus, true);

        Assert.Equal(KeyResolutionKind.NotHandled, Resolve("Ctrl+W").Kind);
        Assert.Equal(KeyResolutionKind.NotHandled, Resolve("Ctrl+K").Kind);
        Assert.Null(_resolver.PendingChord);
    }

    [Fact]
    public void Focused_KeptCommandsStillResolve()
    {
        _context.Set(TerminalFocus, true);

        Assert.Equal("workbench.showCommands", Resolve("Ctrl+Shift+P").Binding?.CommandId);
    }

    [Fact]
    public void RemovedCapture_NoLongerApplies()
    {
        using (_captures.Register(new KeyCapture("other", _ => false)))
        {
            _context.Set("other", true);
            Assert.Equal(KeyResolutionKind.NotHandled, Resolve("Ctrl+W").Kind);
        }

        Assert.Equal("editor.close", Resolve("Ctrl+W").Binding?.CommandId);
    }

    private KeyResolution Resolve(string keys) => _resolver.Resolve(KeySequence.Parse(keys).First, _context);

    private void Bind(string keys, string command) =>
        _registry.Register(new KeybindingDefinition(KeySequence.Parse(keys), command));
}
