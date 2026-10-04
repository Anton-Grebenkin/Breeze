using System.Globalization;
using CodeEditor.Core.Text;

namespace CodeEditor.Core.Tests.Text;

public sealed class PluralTests
{
    private const string RussianForms = "файл|файла|файлов";

    [Theory]
    [InlineData(0, "файлов")]
    [InlineData(1, "файл")]
    [InlineData(2, "файла")]
    [InlineData(4, "файла")]
    [InlineData(5, "файлов")]
    [InlineData(11, "файлов")]
    [InlineData(12, "файлов")]
    [InlineData(14, "файлов")]
    [InlineData(21, "файл")]
    [InlineData(22, "файла")]
    [InlineData(111, "файлов")]
    [InlineData(1001, "файл")]
    public void ThreeForms_FollowRussianRule(long count, string expected) =>
        Assert.Equal(expected, Plural.Select(count, RussianForms));

    [Theory]
    [InlineData(0, "files")]
    [InlineData(1, "file")]
    [InlineData(21, "files")]
    public void TwoForms_FollowEnglishRule(long count, string expected) =>
        Assert.Equal(expected, Plural.Select(count, "file|files"));

    [Fact]
    public void Format_GroupsThousandsByLanguage()
    {
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");
            Assert.Equal("20 000 результатов", Plural.Format(20_000, "результат|результата|результатов"));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal("20,000 results", Plural.Format(20_000, "result|results"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
        }
    }
}
