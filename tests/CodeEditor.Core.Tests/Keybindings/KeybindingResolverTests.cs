using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;

namespace CodeEditor.Core.Tests.Keybindings;

public sealed class KeybindingResolverTests
{
    private static readonly KeyChord CtrlK = Chord("Ctrl+K");
    private static readonly KeyChord CtrlO = Chord("Ctrl+O");
    private static readonly KeyChord CtrlS = Chord("Ctrl+S");
    private static readonly KeyChord CtrlX = Chord("Ctrl+X");

    private readonly KeybindingRegistry _registry = new();
    private readonly ContextKeyService _context = new();
    private readonly KeybindingResolver _resolver;

    public KeybindingResolverTests() => _resolver = new KeybindingResolver(_registry);

    [Fact]
    public void SingleChord_ResolvesToCommand()
    {
        Bind("Ctrl+S", "file.save");

        var result = _resolver.Resolve(CtrlS, _context);

        Assert.Equal(KeyResolutionKind.Command, result.Kind);
        Assert.Equal("file.save", result.Binding?.CommandId);
    }

    [Fact]
    public void UnboundChord_IsNotHandled()
    {
        Assert.Equal(KeyResolutionKind.NotHandled, _resolver.Resolve(CtrlS, _context).Kind);
    }

    [Fact]
    public void TwoChords_WaitThenResolve()
    {
        Bind("Ctrl+K Ctrl+O", "explorer.openFolder");

        var first = _resolver.Resolve(CtrlK, _context);
        var second = _resolver.Resolve(CtrlO, _context);

        Assert.Equal(KeyResolutionKind.WaitingForSecondChord, first.Kind);
        Assert.Equal(CtrlK, first.PendingChord);
        Assert.Equal(KeyResolutionKind.Command, second.Kind);
        Assert.Equal("explorer.openFolder", second.Binding?.CommandId);
        Assert.Null(_resolver.PendingChord);
    }

    [Fact]
    public void WrongSecondChord_ReportsChordNotFoundAndResets()
    {
        Bind("Ctrl+K Ctrl+O", "explorer.openFolder");
        Bind("Ctrl+X", "edit.cut");

        _resolver.Resolve(CtrlK, _context);
        var second = _resolver.Resolve(CtrlX, _context);
        var next = _resolver.Resolve(CtrlX, _context);

        Assert.Equal(KeyResolutionKind.ChordNotFound, second.Kind);
        Assert.Equal(CtrlK, second.PendingChord);
        Assert.Equal("edit.cut", next.Binding?.CommandId);
    }

    [Fact]
    public void Reset_ClearsPendingChord()
    {
        Bind("Ctrl+K Ctrl+O", "explorer.openFolder");
        _resolver.Resolve(CtrlK, _context);

        _resolver.Reset();

        Assert.Null(_resolver.PendingChord);
        Assert.Equal(KeyResolutionKind.NotHandled, _resolver.Resolve(CtrlO, _context).Kind);
    }

    [Fact]
    public void LaterRegistration_Wins()
    {
        Bind("Ctrl+S", "file.save");
        Bind("Ctrl+S", "file.saveAll");

        Assert.Equal("file.saveAll", _resolver.Resolve(CtrlS, _context).Binding?.CommandId);
    }

    [Fact]
    public void WhenCondition_SelectsEnabledBinding()
    {
        Bind("Ctrl+S", "file.save");
        Bind("Ctrl+S", "search.save", "searchFocus");

        Assert.Equal("file.save", _resolver.Resolve(CtrlS, _context).Binding?.CommandId);

        _context.Set("searchFocus", true);

        Assert.Equal("search.save", _resolver.Resolve(CtrlS, _context).Binding?.CommandId);
    }

    [Fact]
    public void DisabledChordBinding_DoesNotStartChord()
    {
        Bind("Ctrl+K Ctrl+O", "explorer.openFolder", "workspaceOpen");

        Assert.Equal(KeyResolutionKind.NotHandled, _resolver.Resolve(CtrlK, _context).Kind);
    }

    [Fact]
    public void DisposedBinding_NoLongerResolves()
    {
        Bind("Ctrl+S", "file.save");
        var registration = Bind("Ctrl+S", "file.saveAll");

        registration.Dispose();

        Assert.Equal("file.save", _resolver.Resolve(CtrlS, _context).Binding?.CommandId);
        Assert.Equal("file.save", _registry.FindForCommand("file.save")?.CommandId);
        Assert.Null(_registry.FindForCommand("file.saveAll"));
    }

    [Fact]
    public void FindForCommand_ReturnsLatestBinding()
    {
        Bind("F1", "palette.show");
        Bind("Ctrl+Shift+P", "palette.show");

        Assert.Equal(KeySequence.Parse("Ctrl+Shift+P"), _registry.FindForCommand("palette.show")?.Sequence);
    }

    private IDisposable Bind(string keys, string commandId, string? when = null) =>
        _registry.Register(new KeybindingDefinition(
            KeySequence.Parse(keys),
            commandId,
            when is null ? null : ContextExpression.Parse(when)));

    private static KeyChord Chord(string text) => KeySequence.Parse(text).First;
}
