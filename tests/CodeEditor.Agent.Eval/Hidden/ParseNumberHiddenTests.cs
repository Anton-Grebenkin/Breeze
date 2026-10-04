using Calc;

namespace Calc.Tests;

public sealed class ParseNumberHiddenTests
{
    [Theory]
    [InlineData(" 42 ", 42)]
    [InlineData("1.5", 1.5)]
    public void ParseNumber_TrimsAndUsesInvariantFormat(string text, double expected) => Assert.Equal(expected, Parser.ParseNumber(text));

    [Fact]
    public void Evaluate_Unchanged() => Assert.Equal(2.5, Parser.Evaluate("10 / 4"));
}
