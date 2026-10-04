using Calc;

namespace Calc.Tests;

public sealed class NegativeHiddenTests
{
    [Theory]
    [InlineData("-2 + 3", 1)]
    [InlineData("4 * -2", -8)]
    [InlineData("-5", -5)]
    [InlineData("-6 / -3", 2)]
    [InlineData("7 - 10", -3)]
    public void Evaluates_NegativeNumbers(string expression, double expected) => Assert.Equal(expected, Parser.Evaluate(expression));
}
