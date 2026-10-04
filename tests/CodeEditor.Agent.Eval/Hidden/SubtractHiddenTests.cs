using Calc;

namespace Calc.Tests;

public sealed class SubtractHiddenTests
{
    [Fact]
    public void Subtract_Renamed() => Assert.Equal(3, Calculator.Subtract(5, 2));

    [Fact]
    public void Parser_StillSubtracts() => Assert.Equal(-3, Parser.Evaluate("7 - 10"));
}
