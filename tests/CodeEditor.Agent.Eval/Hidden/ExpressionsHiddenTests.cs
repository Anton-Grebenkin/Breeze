using Calc;

namespace Calc.Tests;

public sealed class ExpressionsHiddenTests
{
    [Theory]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("-(1 + 2) * 3", -9)]
    [InlineData("2 * -3", -6)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("10 - 4 - 3", 3)]
    [InlineData("((1 + 1) * (2 + 2)) / 4", 2)]
    [InlineData("-5", -5)]
    [InlineData("7 - 10", -3)]
    public void Evaluates_FullExpressions(string expression, double expected) => Assert.Equal(expected, Parser.Evaluate(expression), 10);

    [Theory]
    [InlineData("(1 + 2")]
    [InlineData("2 +")]
    [InlineData("2 3")]
    public void Invalid_Throws(string expression) => Assert.ThrowsAny<ArgumentException>(() => Parser.Evaluate(expression));
}
