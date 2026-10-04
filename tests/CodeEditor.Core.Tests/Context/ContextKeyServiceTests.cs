using CodeEditor.Core.Context;

namespace CodeEditor.Core.Tests.Context;

public sealed class ContextKeyServiceTests
{
    private readonly ContextKeyService _context = new();
    private readonly List<string> _changedKeys = [];

    public ContextKeyServiceTests() => _context.Changed += (_, e) => _changedKeys.Add(e.Key);

    [Fact]
    public void Set_StoresValues()
    {
        _context.Set("flag", true);
        _context.Set("panel", "explorer");

        Assert.Equal(true, _context.GetValue("flag"));
        Assert.Equal("explorer", _context.GetValue("panel"));
        Assert.Null(_context.GetValue("missing"));
    }

    [Fact]
    public void Set_RaisesChangedOnlyWhenValueChanges()
    {
        _context.Set("flag", true);
        _context.Set("flag", true);
        _context.Set("flag", false);
        _context.Set("panel", "explorer");
        _context.Set("panel", "explorer");

        Assert.Equal(["flag", "flag", "panel"], _changedKeys);
    }

    [Fact]
    public void SetNullString_RemovesKey()
    {
        _context.Set("panel", "explorer");

        _context.Set("panel", null);

        Assert.Null(_context.GetValue("panel"));
        Assert.Equal(["panel", "panel"], _changedKeys);
    }

    [Fact]
    public void Remove_MissingKey_DoesNotRaiseChanged()
    {
        _context.Remove("missing");

        Assert.Empty(_changedKeys);
    }

    [Fact]
    public void Evaluate_NullExpression_IsTrue()
    {
        Assert.True(_context.Evaluate(null));
    }
}
