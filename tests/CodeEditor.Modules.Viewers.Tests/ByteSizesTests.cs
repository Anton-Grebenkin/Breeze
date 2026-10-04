using System.Globalization;
using CodeEditor.Modules.Viewers.Services;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// File size: bytes with the right plural form, then 1024-based units with tenths below 100; the exact size alongside.
/// </summary>
public sealed class ByteSizesTests
{
    [Theory]
    [InlineData(1, "1 байт")]
    [InlineData(3, "3 байта")]
    [InlineData(512, "512 байт")]
    public void SmallFiles_AreInBytes(long bytes, string text) => Assert.Equal(text, ByteSizes.Format(bytes));

    [Fact]
    public void LargerFiles_UseUnits_WithTenthsBelowAHundred()
    {
        Assert.Equal(Number(1.5) + " КБ", ByteSizes.Format(1536));
        Assert.Equal(Number(45.2) + " МБ", ByteSizes.Format((long)(45.2 * 1024 * 1024)));
        Assert.Equal("123 МБ", ByteSizes.Format(123L * 1024 * 1024 + 300_000));
        Assert.Equal("2 ТБ", ByteSizes.Format(2L * 1024 * 1024 * 1024 * 1024));
    }

    [Fact]
    public void Exact_AddsTheBytes() =>
        Assert.Equal(Number(2.4) + " МБ (2 516 582 байта)", ByteSizes.FormatExact(2_516_582));

    private static string Number(double value) => value.ToString("0.#", CultureInfo.CurrentCulture);
}
