using System.Text.Json;
using CodeEditor.Core.Settings;

namespace CodeEditor.Core.Tests.Settings;

public sealed class SettingsJsonTests
{
    [Fact]
    public void Parse_DottedAndNestedKeys_CommentsAndTrailingCommas()
    {
        var data = SettingsJson.Parse("""
            // пользовательские настройки
            {
                "editor.fontSize": 16, /* крупнее */
                "editor": { "tabSize": 2 },
                "workbench.colorTheme": "Light+",
                "files.exclude": { "**/*.tmp": true },
                "search.include": ["src", "tests"],
            }
            """);

        Assert.Equal("16", data["editor:fontSize"]);
        Assert.Equal("2", data["editor:tabSize"]);
        Assert.Equal("Light+", data["workbench:colorTheme"]);
        Assert.Equal("true", data["files:exclude:**/*.tmp"]);
        Assert.Equal("tests", data["search:include:1"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Empty_IsEmpty(string json) => Assert.Empty(SettingsJson.Parse(json));

    [Theory]
    [InlineData("{ \"a\": ")]
    [InlineData("[1, 2]")]
    public void Parse_Invalid_Throws(string json) => Assert.ThrowsAny<JsonException>(() => SettingsJson.Parse(json));

    [Fact]
    public void SetValue_ReplacesExistingValue_KeepingComments()
    {
        const string json = "{\n    // тема\n    \"workbench.colorTheme\": \"Dark+\", // тёмная\n    \"editor.fontSize\": 14\n}\n";

        var updated = SettingsJson.SetValue(json, "workbench.colorTheme", "Light+");

        Assert.Equal("{\n    // тема\n    \"workbench.colorTheme\": \"Light+\", // тёмная\n    \"editor.fontSize\": 14\n}\n", updated);
    }

    [Fact]
    public void SetValue_AppendsNewKey_AfterLastValue()
    {
        var updated = SettingsJson.SetValue("{\n    \"editor.fontSize\": 14 // размер\n}\n", "editor.tabSize", 2);

        Assert.Equal("{\n    \"editor.fontSize\": 14,\n    \"editor.tabSize\": 2 // размер\n}\n", updated);
        Assert.Equal("2", SettingsJson.Parse(updated)["editor:tabSize"]);
    }

    [Fact]
    public void SetValue_IntoEmptyObjectOrFile()
    {
        Assert.Equal("{\n    \"a\": true\n}\n", SettingsJson.SetValue(string.Empty, "a", true));
        Assert.Equal("true", SettingsJson.Parse(SettingsJson.SetValue("// шапка\n{\n}\n", "a", true))["a"]);
    }

    [Fact]
    public void SetValue_ReplacesObjectValue_AndWritesCyrillicReadably()
    {
        var updated = SettingsJson.SetValue("{ \"x\": { \"nested\": [1, 2] }, \"y\": 1 }", "x", "значение");

        Assert.Equal("{ \"x\": \"значение\", \"y\": 1 }", updated);
    }

    [Fact]
    public void SetValue_Broken_Throws() =>
        Assert.ThrowsAny<JsonException>(() => SettingsJson.SetValue("{ \"a\": 1 ", "b", 2));

    [Fact]
    public void RemoveValue_Middle_TakesItsComma()
    {
        var updated = SettingsJson.RemoveValue("{\n    \"a\": 1,\n    \"b\": 2,\n    \"c\": 3\n}\n", "b");

        Assert.Equal("{\n    \"a\": 1,\n    \"c\": 3\n}\n", updated);
    }

    [Fact]
    public void RemoveValue_LastOrOnly_KeepsValidJson()
    {
        Assert.Equal("{\n    \"a\": 1\n}\n", SettingsJson.RemoveValue("{\n    \"a\": 1,\n    \"b\": { \"x\": 2 }\n}\n", "b"));
        Assert.Empty(SettingsJson.Parse(SettingsJson.RemoveValue("{\n    \"a\": 1\n}\n", "a")));
    }

    [Fact]
    public void RemoveValue_MissingKey_ReturnsTextAsIs()
    {
        const string json = "{\n    // комментарий\n    \"a\": 1\n}\n";

        Assert.Same(json, SettingsJson.RemoveValue(json, "b"));
        Assert.ThrowsAny<JsonException>(() => SettingsJson.RemoveValue("{ \"a\": 1 ", "b"));
    }
}
