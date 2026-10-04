using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Search.Services.Agent;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Search.Tests;

/// <summary>Code names from the user's message are searched up front, so the first model request knows their files.</summary>
public sealed class RelatedCodeContextTests : IDisposable
{
    private readonly SearchFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData("почему DeleteCategory падает с column v.category_record_id does not exist?", "DeleteCategory|v.category_record_id")]
    [InlineData("посмотри `Helper.Run()` и Program.cs", "Helper.Run()|Program.cs")]
    [InlineData("MAX_RETRY_COUNT и userId", "MAX_RETRY_COUNT|userId")]
    [InlineData("просто вопрос про проект без имён", "")]
    [InlineData("e.g. a.b и ab", "")]
    public void Find_TakesCodeNames_NotWords(string text, string expected) =>
        Assert.Equal(expected.Split('|', StringSplitOptions.RemoveEmptyEntries), MentionedIdentifiers.Find(text));

    [Fact]
    public void Find_LimitsCount_AndSkipsRepeats() =>
        Assert.Equal(MentionedIdentifiers.MaxIdentifiers, MentionedIdentifiers.Find("alphaBeta alphaBeta gammaDelta epsZeta etaTheta iotaKappa lambdaMu nuXi").Count);

    // Files per name; a name absent from the code is listed separately, not mixed with found ones.
    [Fact]
    public async Task Context_ListsFilesPerName_AndNamesMissingFromCode()
    {
        await _fixture.Index.WhenReady;
        var context = new RelatedCodeContext(_fixture.Search, _fixture.Documents, new InlineUiDispatcher());

        var lines = await context.GetContextAsync(new AgentContextRequest(false, "почему DeleteHelper падает, а Helper.Run() не вызывается?"), TestContext.Current.CancellationToken);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Имена из запроса, найденные в коде (буквальные совпадения — зацепки для проверки, а не доказательство причины): Helper.Run — src/Program.cs (1).", lines[0]);
        Assert.StartsWith("В коде нигде нет: DeleteHelper — вероятно, собираются во время работы", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Context_IsEmpty_WithoutNames()
    {
        await _fixture.Index.WhenReady;
        var context = new RelatedCodeContext(_fixture.Search, _fixture.Documents, new InlineUiDispatcher());

        Assert.Empty(await context.GetContextAsync(new AgentContextRequest(false, "расскажи о проекте"), TestContext.Current.CancellationToken));
    }
}
