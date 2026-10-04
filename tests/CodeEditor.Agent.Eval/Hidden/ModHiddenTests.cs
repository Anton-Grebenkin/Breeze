using Calc;

namespace Calc.Tests;

public sealed class ModHiddenTests
{
    [Fact]
    public void Mod_Remainder() => Assert.Equal(1, Calculator.Mod(7, 3));

    [Fact]
    public void Mod_ByZero_Throws() => Assert.Throws<DivideByZeroException>(() => Calculator.Mod(1, 0));

    [Fact]
    public void Parser_Mod() => Assert.Equal(1, Parser.Evaluate("7 % 3"));
}
