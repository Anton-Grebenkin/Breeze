using BenchmarkDotNet.Attributes;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;

namespace CodeEditor.Benchmarks;

/// <summary>
/// Key press resolution with 500 bindings: a single chord, a two-chord sequence and an unbound key.
/// </summary>
[MemoryDiagnoser]
public class KeybindingResolverBenchmarks
{
    private const int BindingCount = 500;

    private static readonly KeyChord CtrlS = new(KeyModifiers.Ctrl, KeyCode.S);
    private static readonly KeyChord CtrlK = new(KeyModifiers.Ctrl, KeyCode.K);
    private static readonly KeyChord CtrlO = new(KeyModifiers.Ctrl, KeyCode.O);
    private static readonly KeyChord PlainA = new(KeyModifiers.None, KeyCode.A);

    private readonly ContextKeyService _context = new();
    private KeybindingResolver _resolver = null!;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new KeybindingRegistry();
        var when = ContextExpression.Parse("editorFocus && !readOnly");
        var keys = Enum.GetValues<KeyCode>().Where(key => key != KeyCode.None).ToArray();
        var modifiers = new[] { KeyModifiers.Ctrl, KeyModifiers.Alt, KeyModifiers.Ctrl | KeyModifiers.Shift, KeyModifiers.Ctrl | KeyModifiers.Alt };

        for (var i = 0; i < BindingCount; i++)
        {
            var chord = new KeyChord(modifiers[i % modifiers.Length], keys[i % keys.Length]);
            registry.Register(new KeybindingDefinition(new KeySequence(chord), $"command.{i}", i % 2 == 0 ? when : null));
        }

        registry.Register(new KeybindingDefinition(new KeySequence(CtrlS), "file.save", when));
        registry.Register(new KeybindingDefinition(new KeySequence(CtrlK, CtrlO), "explorer.openFolder"));

        _context.Set("editorFocus", true);
        _resolver = new KeybindingResolver(registry);
    }

    [Benchmark]
    public KeyResolution SingleChord() => _resolver.Resolve(CtrlS, _context);

    [Benchmark]
    public KeyResolution TwoChords()
    {
        _resolver.Resolve(CtrlK, _context);
        return _resolver.Resolve(CtrlO, _context);
    }

    [Benchmark]
    public KeyResolution Unbound() => _resolver.Resolve(PlainA, _context);
}
