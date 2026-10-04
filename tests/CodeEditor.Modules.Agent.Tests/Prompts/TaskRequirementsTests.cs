using CodeEditor.Modules.Agent.Services.Prompts;

namespace CodeEditor.Modules.Agent.Tests.Prompts;

/// <summary>A request has several requirements when it's a list of three items or a long text (ADR 0016).</summary>
public sealed class TaskRequirementsTests
{
    [Theory]
    [InlineData("исправь падающий тест", false)]
    [InlineData("Правила:\n1. код без учёта регистра\n2) скидка до НДС\n3. один код на заказ", true)]
    [InlineData("Сделай:\n- A\n* B\n• C", true)]
    [InlineData("Сделай:\n- A\n- B", false)]
    [InlineData("-A\n-B\n-C", false)]
    [InlineData("2024. год\n100. строк\n3.14 пи", false)]
    [InlineData(null, false)]
    public void ListItems_MakeSeveralRequirements(string? request, bool expected) =>
        Assert.Equal(expected, TaskRequirements.AreSeveral(request));

    [Theory]
    [InlineData("Готово.", false)]
    [InlineData("Правка в src/A.cs:12.", false)]
    [InlineData("| скидка | src/Pricing.cs:10 | тест Tax |\n| код | src\\Promo.cs:4 | сборка |", true)]
    [InlineData("Встреча в 12:30, версия 1.2:3", false)]
    public void Evidence_IsTwoPathLineReferences(string report, bool expected) =>
        Assert.Equal(expected, TaskRequirements.CitesEvidence(report));

    [Fact]
    public void LongRequest_HasSeveralRequirements() =>
        Assert.True(TaskRequirements.AreSeveral(new string('а', TaskRequirements.LongRequest)));
}
