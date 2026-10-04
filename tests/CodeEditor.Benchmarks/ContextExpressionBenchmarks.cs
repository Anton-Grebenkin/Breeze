using BenchmarkDotNet.Attributes;
using CodeEditor.Core.Context;

namespace CodeEditor.Benchmarks;

/// <summary>
/// A condition is parsed once at registration and evaluated on every key press and palette refresh.
/// </summary>
[MemoryDiagnoser]
public class ContextExpressionBenchmarks
{
    private const string Text = "editorFocus && !readOnly && (activePanel == 'explorer' || resourceExtname == .cs)";

    private readonly ContextKeyService _context = new();
    private ContextExpression _expression = null!;

    [GlobalSetup]
    public void Setup()
    {
        _context.Set("editorFocus", true);
        _context.Set("readOnly", false);
        _context.Set("activePanel", "search");
        _context.Set("resourceExtname", ".cs");
        _expression = ContextExpression.Parse(Text);
    }

    [Benchmark]
    public ContextExpression Parse() => ContextExpression.Parse(Text);

    [Benchmark]
    public bool Evaluate() => _expression.Evaluate(_context);
}
