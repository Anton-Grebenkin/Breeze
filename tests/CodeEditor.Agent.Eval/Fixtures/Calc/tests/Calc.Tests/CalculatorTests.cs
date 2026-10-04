using Calc;

namespace Calc.Tests;

public sealed class CalculatorTests
{
    [Fact]
    public void Add_And_Sub() => Assert.Equal((5, -1), (Calculator.Add(2, 3), Calculator.Sub(2, 3)));

    [Fact]
    public void Divide_ByZero_Throws() => Assert.Throws<DivideByZeroException>(() => Calculator.Divide(1, 0));

    [Fact]
    public void Percent_OfValue() => Assert.Equal(20, Calculator.Percent(200, 10));
}
