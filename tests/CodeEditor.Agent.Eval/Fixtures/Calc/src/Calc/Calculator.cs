namespace Calc;

/// <summary>Арифметика калькулятора.</summary>
public static class Calculator
{
    public static double Add(double a, double b) => a + b;

    public static double Sub(double a, double b) => a - b;

    public static double Multiply(double a, double b) => a * b;

    /// <exception cref="DivideByZeroException">Делитель равен нулю.</exception>
    public static double Divide(double a, double b) =>
        b == 0 ? throw new DivideByZeroException("Деление на ноль.") : a / b;

    /// <summary>Процент от числа: <c>Percent(200, 10)</c> = 20.</summary>
    public static double Percent(double value, double percent) => value * percent / 100;
}
