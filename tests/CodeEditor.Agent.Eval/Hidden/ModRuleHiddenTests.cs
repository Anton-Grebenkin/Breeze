using Calc;

namespace Calc.Tests;

public sealed class ModRuleHiddenTests
{
    [Fact]
    public void Mod_Remainder() => Assert.Equal(1, Calculator.Mod(7, 3));

    [Fact]
    public void Mod_ByZero_HasRulePrefix() =>
        Assert.StartsWith("CALC-E: ", Assert.Throws<DivideByZeroException>(() => Calculator.Mod(1, 0)).Message, StringComparison.Ordinal);
}
