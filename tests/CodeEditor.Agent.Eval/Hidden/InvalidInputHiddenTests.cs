using Calc;

namespace Calc.Tests;

public sealed class InvalidInputHiddenTests
{
    [Theory]
    [InlineData("2 +")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    public void Invalid_ThrowsArgumentException(string expression)
    {
        var error = Assert.Throws<ArgumentException>(() => Parser.Evaluate(expression));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("42", 42)]
    public void Valid_StillWork(string expression, double expected) => Assert.Equal(expected, Parser.Evaluate(expression));
}
