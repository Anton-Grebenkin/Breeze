using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Documents.Services;
using CodeEditor.Modules.Documents.Services.Changes;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.AI;
using static CodeEditor.Modules.Documents.Tests.DocumentsFixture;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Agent tools round trip: create a document with <c>document_change</c> and read it back with <c>document</c> (Word,
/// Excel, PDF); Word and Excel edits; PDF merge and page extraction; approval cards showing what will change.
/// </summary>
public sealed class DocumentAgentToolsTests : IDisposable
{
    private readonly DocumentsFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Word_CreateThenRead_GivesTheMarkdownBack()
    {
        var result = await _fixture.ChangeAsync(DocumentActions.CreateDocx, "docs/Отчёт.docx", new() { ["markdown"] = "# Отчёт\n\nТекст с **жирным**.\n\n- пункт" });

        Assert.Contains("docs/Отчёт.docx", result, StringComparison.Ordinal);
        var text = await _fixture.ReadAsync("docs/Отчёт.docx");
        Assert.Equal("# Отчёт\n\nТекст с **жирным**.\n\n- пункт\n", text);
    }

    // The user sees the result right away: the document opens in a preview tab.
    [Fact]
    public async Task Change_OpensTheDocumentInAPreviewTab()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateDocx, "a.docx", new() { ["markdown"] = "Текст" });

        var (id, argument) = Assert.Single(_fixture.Commands.Executed);
        Assert.Equal(ShellCommandIds.OpenFile, id);
        Assert.Equal(new OpenFileRequest(PathOf("a.docx"), Preview: true), argument);
    }

    [Fact]
    public async Task Word_ReplaceAndInsert_ChangeTheFile()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateDocx, "a.docx", new() { ["markdown"] = "Срок: **1 мая**.\n\nКонец." });

        await _fixture.ChangeAsync(DocumentActions.ReplaceText, "a.docx", new() { ["find"] = "**1 мая**", ["replace"] = "15 июня" });
        await _fixture.ChangeAsync(DocumentActions.Insert, "a.docx", new() { ["markdown"] = "## Итоги", ["after"] = "Срок" });

        Assert.Equal("Срок: **15 июня**.\n\n## Итоги\n\nКонец.\n", await _fixture.ReadAsync("a.docx"));
    }

    [Fact]
    public async Task Excel_CreateWriteCellsAndRead()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateXlsx, "budget.xlsx", new()
        {
            ["sheets"] = Json("""[{"name": "Бюджет", "rows": [["Статья", "Сумма"], ["Аренда", 1200], ["Итого", "=SUM(B2:B2)"]]}]"""),
        });

        await _fixture.ChangeAsync(DocumentActions.SetCells, "budget.xlsx", new() { ["cells"] = Json("""[{"cell": "B2", "value": "1500"}, {"cell": "C1", "value": "Комментарий"}]""") });

        var text = await _fixture.ReadAsync("budget.xlsx", new() { ["range"] = "A1:C3" });
        Assert.Contains("| 1 | Статья | Сумма | Комментарий |", text, StringComparison.Ordinal);
        Assert.Contains("| 2 | Аренда | 1500 |  |", text, StringComparison.Ordinal);
        Assert.Contains("| 3 | Итого | =SUM(B2:B2) |  |", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Excel_AddAndRenameSheet()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateXlsx, "b.xlsx", new() { ["sheets"] = Json("""[{"name": "Данные", "rows": [["x"]]}]""") });

        await _fixture.ChangeAsync(DocumentActions.AddSheet, "b.xlsx", new() { ["sheet"] = "Итоги", ["rows"] = Json("""[["=Данные!A1"]]""") });
        await _fixture.ChangeAsync(DocumentActions.RenameSheet, "b.xlsx", new() { ["sheet"] = "Данные", ["newName"] = "Исходные" });

        var text = await _fixture.ReadAsync("b.xlsx", new() { ["sheet"] = "Итоги" });
        Assert.Contains("Листы: Исходные (A1), Итоги (A1)", text, StringComparison.Ordinal);
        Assert.Contains("=Исходные!A1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pdf_CreateReadMergeAndExtract()
    {
        await _fixture.ChangeAsync(DocumentActions.CreatePdf, "a.pdf", new() { ["markdown"] = "# Первый документ\n\nКириллица на месте." });
        await _fixture.ChangeAsync(DocumentActions.CreatePdf, "b.pdf", new() { ["markdown"] = "# Второй документ" });

        await _fixture.ChangeAsync(DocumentActions.MergePdf, "all.pdf", new() { ["sources"] = Json("""[{"path": "a.pdf"}, {"path": "b.pdf", "pages": "1"}]""") });
        await _fixture.ChangeAsync(DocumentActions.ExtractPages, "second.pdf", new() { ["source"] = "all.pdf", ["pages"] = "2" });

        var all = await _fixture.ReadAsync("all.pdf");
        Assert.Contains("all.pdf: PDF, страниц: 2.", all, StringComparison.Ordinal);
        Assert.Contains("Кириллица на месте.", all, StringComparison.Ordinal);
        var second = await _fixture.ReadAsync("second.pdf");
        Assert.Contains("Второй документ", second, StringComparison.Ordinal);
        Assert.DoesNotContain("Первый", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PresentationAndCsv_AreRead()
    {
        _fixture.Files.AddBytes(PathOf("deck.pptx"), TestPresentations.TwoSlides());
        _fixture.Files.AddBytes(PathOf("data.csv"), "Город;Жители\nТула;470000\n"u8.ToArray());

        var slides = await _fixture.ReadAsync("deck.pptx", new() { ["pages"] = "2" });
        var csv = await _fixture.ReadAsync("data.csv");

        Assert.StartsWith("## Слайд 2: Планы", slides, StringComparison.Ordinal);
        Assert.Contains("| 2 | Тула | 470000 |", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_OfANewDocument_ShowsItsText()
    {
        var preview = await _fixture.PreviewAsync(DocumentActions.CreateDocx, "new.docx", new() { ["markdown"] = "# Заголовок" });

        Assert.Equal((ProposedChangeKind.Create, "new.docx", "# Заголовок\n"), (preview.Kind, preview.RelativePath, preview.NewText));
        Assert.Equal("Агент хочет записать документ Word", preview.Title);
        Assert.False(_fixture.Files.FileExists(PathOf("new.docx")), "Предпросмотр ничего не пишет.");
    }

    [Fact]
    public async Task Preview_OfCells_ShowsWasAndWillBe()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateXlsx, "c.xlsx", new() { ["sheets"] = Json("""[{"name": "Л", "rows": [["a", 1]]}]""") });

        var preview = await _fixture.PreviewAsync(DocumentActions.SetCells, "c.xlsx", new() { ["start"] = "B1", ["rows"] = Json("[[2, \"=B1*2\"]]") });

        Assert.Equal(ProposedChangeKind.Edit, preview.Kind);
        Assert.Equal("B1: 1\nC1: (пусто)\n", preview.OldText);
        Assert.Equal("B1: 2\nC1: =B1*2\n", preview.NewText);
    }

    [Theory]
    [InlineData(@"..\outside.docx")]
    [InlineData(@"C:\other\a.docx")]
    public async Task PathsOutsideTheWorkspace_AreRefused(string path)
    {
        await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ReadAsync(path));
        await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ChangeAsync(DocumentActions.CreateDocx, path, new() { ["markdown"] = "x" }));
    }

    [Fact]
    public async Task ServiceFolders_AreNotWritten() =>
        await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ChangeAsync(DocumentActions.CreateDocx, "bin/a.docx", new() { ["markdown"] = "x" }));

    // Full replacement requires overwrite, and the old file goes to the Recycle Bin so it can be restored.
    [Fact]
    public async Task Create_OverAnExistingFile_NeedsOverwrite_AndRecyclesTheOldOne()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateDocx, "a.docx", new() { ["markdown"] = "Первый" });

        var error = await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ChangeAsync(DocumentActions.CreateDocx, "a.docx", new() { ["markdown"] = "Второй" }));
        var result = await _fixture.ChangeAsync(DocumentActions.CreateDocx, "a.docx", new() { ["markdown"] = "Второй", ["overwrite"] = true });

        Assert.Contains("overwrite", error.Message, StringComparison.Ordinal);
        Assert.Contains("корзин", result, StringComparison.Ordinal);
        Assert.Equal([PathOf("a.docx")], _fixture.Files.RecycledPaths);
        Assert.Equal("Второй\n", await _fixture.ReadAsync("a.docx"));
    }

    // The user approved the diff of the version they saw; if the document changed since, nothing is written.
    [Fact]
    public async Task DocumentChangedAfterTheCard_IsNotOverwritten()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateDocx, "a.docx", new() { ["markdown"] = "Срок: май." });
        Dictionary<string, object?> replace = new() { ["find"] = "май", ["replace"] = "июнь" };
        await _fixture.PreviewAsync(DocumentActions.ReplaceText, "a.docx", replace);

        _fixture.Files.WriteAllBytesAtomic(PathOf("a.docx"), Formats.Word.DocxWriter.Create(Formats.MarkdownReader.Read("Срок: май, пятница.")));
        var error = await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ChangeAsync(DocumentActions.ReplaceText, "a.docx", replace));

        Assert.Contains("a.docx", error.Message, StringComparison.Ordinal);
        Assert.Equal("Срок: май, пятница.\n", await _fixture.ReadAsync("a.docx"));
    }

    // The card failed to build but the write would now succeed: without a card the user never saw the change.
    [Fact]
    public async Task ChangeWhoseCardFailed_IsNotWritten()
    {
        Dictionary<string, object?> insert = new() { ["markdown"] = "Новое" };
        await Assert.ThrowsAsync<AgentToolException>(() => _fixture.PreviewAsync(DocumentActions.Insert, "late.docx", insert));

        _fixture.Files.AddBytes(PathOf("late.docx"), Formats.Word.DocxWriter.Create(Formats.MarkdownReader.Read("Было")));
        var error = await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ChangeAsync(DocumentActions.Insert, "late.docx", insert));
        await _fixture.ChangeAsync(DocumentActions.Insert, "late.docx", insert);

        Assert.Contains("карточку", error.Message, StringComparison.Ordinal);
        Assert.Equal("Было\n\nНовое\n", await _fixture.ReadAsync("late.docx"));
    }

    [Fact]
    public async Task ApprovedCard_IsWritten()
    {
        await _fixture.ChangeAsync(DocumentActions.CreateDocx, "a.docx", new() { ["markdown"] = "Срок: май." });
        Dictionary<string, object?> replace = new() { ["find"] = "май", ["replace"] = "июнь" };

        await _fixture.PreviewAsync(DocumentActions.ReplaceText, "a.docx", replace);
        await _fixture.ChangeAsync(DocumentActions.ReplaceText, "a.docx", replace);

        Assert.Equal("Срок: июнь.\n", await _fixture.ReadAsync("a.docx"));
    }

    [Theory]
    [InlineData(DocumentActions.CreateDocx, "a.pdf")]
    [InlineData(DocumentActions.SetCells, "a.docx")]
    [InlineData("delete", "a.docx")]
    public async Task WrongActionOrFormat_IsExplained(string action, string path) =>
        await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ChangeAsync(action, path, new() { ["markdown"] = "x" }));

    [Fact]
    public async Task UnsupportedOrBrokenFiles_AreExplained()
    {
        _fixture.Files.AddFile(PathOf("broken.docx"), "не zip");
        _fixture.Files.AddFile(PathOf("old.doc"), "x");

        var broken = await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ReadAsync("broken.docx"));
        var old = await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ReadAsync("old.doc"));

        Assert.Contains("broken.docx", broken.Message, StringComparison.Ordinal);
        Assert.Contains("read_file", old.Message, StringComparison.Ordinal);
    }

    // Broken XML inside the package fails on read, not on open; the model still gets an answer.
    [Fact]
    public async Task BrokenXmlInside_IsAnAnswerForTheModel()
    {
        _fixture.Files.AddBytes(PathOf("broken.docx"), TestDocuments.WordWithBrokenXml());

        var read = await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ReadAsync("broken.docx"));
        var change = await Assert.ThrowsAsync<AgentToolException>(() => _fixture.ChangeAsync(DocumentActions.Insert, "broken.docx", new() { ["markdown"] = "x" }));

        Assert.Contains("broken.docx", read.Message, StringComparison.Ordinal);
        Assert.Contains("broken.docx", change.Message, StringComparison.Ordinal);
    }

    // No "any value" ({}) in the schema so every model service accepts it; cell values are strings, like Excel input.
    [Fact]
    public void ChangeSchema_TypesCellValuesAsText()
    {
        var tool = _fixture.Tools.CreateTools().OfType<AIFunction>().Single(candidate => candidate.Name == DocumentAgentTools.ChangeName);
        var schema = tool.JsonSchema.GetRawText();
        var properties = tool.JsonSchema.GetProperty("properties");

        Assert.DoesNotContain("{}", schema.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal("string", properties.GetProperty("cells").GetProperty("items").GetProperty("properties").GetProperty("value").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("rows").GetProperty("items").GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public void ReadTool_IsReadOnly_ChangeToolNeedsApproval()
    {
        var tools = _fixture.Tools.CreateTools().ToList();

        Assert.True(ReadOnlyAIFunction.IsReadOnly(tools.Single(tool => tool.Name == DocumentAgentTools.ReadName)));
        Assert.NotNull(tools.Single(tool => tool.Name == DocumentAgentTools.ChangeName).GetService<ApprovalRequiredAIFunction>());
    }
}
