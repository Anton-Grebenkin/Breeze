using Calc;

namespace Calc.Tests;

public sealed class PrecedenceHiddenTests
{
    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("10 - 4 - 3", 3)]
    [InlineData("8 / 2 / 2", 2)]
    [InlineData("1 + 2 + 3 + 4", 10)]
    [InlineData("2 * 3 + 4 * 5", 26)]
    [InlineData("20 - 6 / 3", 18)]
    [InlineData("7", 7)]
    [InlineData("10 / 4", 2.5)]
    public void Evaluates_WithPrecedence(string expression, double expected) => Assert.Equal(expected, Parser.Evaluate(expression), 10);
}
