using CodeEditor.Modules.Agent.Services.Turn;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>A tool call written as text instead of a function call, in various vendors' native formats.</summary>
public sealed class TextToolCallTests
{
    [Theory]
    [InlineData("<function=search_text> <parameter=query> MetadataLocalizerInterceptor </parameter> </function> </tool_call>", true)]
    [InlineData("<tool_call>{\"name\": \"read_file\"}</tool_call>", true)]
    [InlineData("<invoke name=\"read_file\"><parameter name=\"path\">a.cs</parameter></invoke>", true)]
    [InlineData("[TOOL_CALLS][{\"name\": \"read_file\", \"arguments\": {}}]", true)]
    [InlineData("{\"name\": \"search_text\", \"arguments\": {\"query\": \"x\"}}", true)]
    [InlineData("Готово: метод `Evaluate` в src/Calc/Parser.cs:12 теперь бросает ArgumentException.", false)]
    [InlineData("В JSON конфигурации поле \"name\" задаёт имя модуля.", false)]
    [InlineData("", false)]
    public void Detects_NativeCallFormats_NotOrdinaryAnswers(string report, bool expected) =>
        Assert.Equal(expected, TextToolCall.IsIn(report));
}
