using Calc;

namespace Calc.Tests;

public sealed class DivideHiddenTests
{
    [Fact]
    public void Divide_ByZero_Throws() => Assert.Throws<DivideByZeroException>(() => Calculator.Divide(1, 0));

    [Fact]
    public void Divide_Regular() => Assert.Equal(2.5, Calculator.Divide(5, 2));
}
