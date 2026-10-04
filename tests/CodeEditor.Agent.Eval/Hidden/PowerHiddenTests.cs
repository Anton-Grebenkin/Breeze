using Calc;

namespace Calc.Tests;

public sealed class PowerHiddenTests
{
    [Theory]
    [InlineData(2, 3, 8)]
    [InlineData(2, -1, 0.5)]
    [InlineData(5, 0, 1)]
    [InlineData(-3, 3, -27)]
    public void Power_IntegerExponent(double x, int n, double expected) => Assert.Equal(expected, Calculator.Power(x, n), 10);
}
