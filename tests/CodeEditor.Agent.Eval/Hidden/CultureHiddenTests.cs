using System.Globalization;
using Calc;

namespace Calc.Tests;

public sealed class CultureHiddenTests
{
    [Fact]
    public void Evaluate_DoesNotDependOnCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        try
        {
            Assert.Equal(3.5, Parser.Evaluate("2.5 + 1"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
