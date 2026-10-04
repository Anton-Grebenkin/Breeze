using System.Globalization;

namespace Calc;

/// <summary>Разбор и вычисление выражения вида «a оп b»: <c>2 + 3</c>, <c>10 / 4</c>.</summary>
public static class Parser
{
    private static readonly char[] Operators = ['+', '-', '*', '/'];

    public static double Evaluate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var index = expression.IndexOfAny(Operators);
        if (index < 0)
        {
            return Number(expression);
        }

        var left = Number(expression[..index]);
        var right = Number(expression[(index + 1)..]);
        return expression[index] switch
        {
            '+' => Calculator.Add(left, right),
            '-' => Calculator.Sub(left, right),
            '*' => Calculator.Multiply(left, right),
            _ => Calculator.Divide(left, right),
        };
    }

    private static double Number(string text) => double.Parse(text.Trim(), CultureInfo.InvariantCulture);
}
