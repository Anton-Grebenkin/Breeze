using System.Globalization;
using CodeEditor.Core.Localization;

namespace CodeEditor.Core.Tests.Localization;

public sealed class UiLanguageTests
{
    [Theory]
    [InlineData(null, "ru-RU", "ru")]
    [InlineData("auto", "ru-RU", "ru")]
    [InlineData("auto", "de-DE", "en")]
    [InlineData("en", "ru-RU", "en")]
    [InlineData(" RU ", "en-US", "ru")]
    [InlineData("fr", "ru-RU", "en")]
    public void Resolve_SettingOrSystemLanguage_EnglishOtherwise(string? setting, string system, string expected) =>
        Assert.Equal(expected, UiLanguage.Resolve(setting, CultureInfo.GetCultureInfo(system)).Name);

    [Fact]
    public void ReadSetting_FromSettingsFile_BrokenFileMeansNoSetting()
    {
        Assert.Equal("en", UiLanguage.ReadSetting("// комментарий\n{ \"workbench.language\": \"en\", }"));
        Assert.Null(UiLanguage.ReadSetting("{ \"editor.fontSize\": 14 }"));
        Assert.Null(UiLanguage.ReadSetting("{ сломано"));
    }
}
