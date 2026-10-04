using CodeEditor.Core.Context;

namespace CodeEditor.Core.Tests.Context;

public sealed class ContextExpressionTests
{
    private readonly ContextKeyService _context = new();

    public ContextExpressionTests()
    {
        _context.Set("editorFocus", true);
        _context.Set("readOnly", false);
        _context.Set("activePanel", "explorer");
        _context.Set("resourceExtname", ".cs");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("editorFocus", true)]
    [InlineData("readOnly", false)]
    [InlineData("unknownKey", false)]
    [InlineData("activePanel", true)]
    [InlineData("!readOnly", true)]
    [InlineData("!!editorFocus", true)]
    [InlineData("editorFocus && !readOnly", true)]
    [InlineData("editorFocus && readOnly", false)]
    [InlineData("readOnly || editorFocus", true)]
    [InlineData("activePanel == explorer", true)]
    [InlineData("activePanel == 'explorer'", true)]
    [InlineData("activePanel != explorer", false)]
    [InlineData("activePanel == 'search view'", false)]
    [InlineData("resourceExtname == .cs", true)]
    [InlineData("readOnly == false", true)]
    [InlineData("editorFocus == true", true)]
    [InlineData("unknownKey == ''", false)]
    [InlineData("unknownKey != x", true)]
    [InlineData("  editorFocus  ", true)]
    public void Evaluate_ReturnsExpectedValue(string text, bool expected)
    {
        var expression = ContextExpression.Parse(text);

        Assert.Equal(expected, expression.Evaluate(_context));
    }

    [Theory]
    [InlineData("readOnly && editorFocus || true", true)]
    [InlineData("true || editorFocus && readOnly", true)]
    [InlineData("readOnly && (editorFocus || true)", false)]
    [InlineData("(readOnly || editorFocus) && !readOnly", true)]
    [InlineData("!(editorFocus && readOnly)", true)]
    public void Evaluate_AndBindsTighterThanOr(string text, bool expected)
    {
        Assert.Equal(expected, ContextExpression.Parse(text).Evaluate(_context));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 3)]
    [InlineData("editorFocus &&", 14)]
    [InlineData("&& editorFocus", 0)]
    [InlineData("(editorFocus", 12)]
    [InlineData("editorFocus)", 11)]
    [InlineData("activePanel ==", 14)]
    [InlineData("activePanel == 'explorer", 15)]
    [InlineData("editorFocus readOnly", 12)]
    [InlineData("!= x", 0)]
    public void Parse_InvalidExpression_ReportsPosition(string text, int position)
    {
        var exception = Assert.Throws<ContextExpressionException>(() => ContextExpression.Parse(text));

        Assert.Equal(position, exception.Position);
        Assert.Equal(text, exception.Expression);
    }

    [Fact]
    public void Evaluate_ReflectsContextChanges()
    {
        var expression = ContextExpression.Parse("editorFocus && !readOnly");

        _context.Set("readOnly", true);

        Assert.False(expression.Evaluate(_context));
    }
}
