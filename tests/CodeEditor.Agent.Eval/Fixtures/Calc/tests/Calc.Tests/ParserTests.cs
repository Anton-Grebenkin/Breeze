using Calc;

namespace Calc.Tests;

public sealed class ParserTests
{
    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("7 - 10", -3)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("42", 42)]
    public void Evaluates_BinaryExpressions(string expression, double expected) => Assert.Equal(expected, Parser.Evaluate(expression));

    [Fact]
    public void Parses_Multiplication() => Assert.Equal(6, Parser.Evaluate("2 * 3"));
}
