using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Word;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Word: Markdown → .docx → blocks and back to Markdown (headings, lists, tables, emphasis, Cyrillic), text replacement
/// that keeps run formatting, inserting blocks after a matched paragraph and into a foreign document without styles.
/// </summary>
public sealed class WordDocumentTests
{
    private const string Sample = """
        # Отчёт за квартал

        Выручка выросла на **12 %**, а *расходы* снизились. Подробности — на [сайте](https://example.com/report).

        ## Задачи

        - Подготовить смету
          - Согласовать с бухгалтерией
        - Отправить клиенту

        1. Первый шаг
        2. Второй шаг

        | Статья | Сумма |
        | --- | --- |
        | Аренда | 1 200 |
        | Связь | 300 |

        ```
        var итог = 1500;
        ```

        > Цитата руководителя
        """;

    [Fact]
    public void Markdown_RoundTripsThroughWord()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read(Sample));

        var markdown = MarkdownWriter.Write(DocxReader.Read(bytes));

        Assert.Contains("# Отчёт за квартал", markdown, StringComparison.Ordinal);
        Assert.Contains("## Задачи", markdown, StringComparison.Ordinal);
        Assert.Contains("Выручка выросла на **12 %**, а *расходы* снизились. Подробности — на [сайте](https://example.com/report).", markdown, StringComparison.Ordinal);
        Assert.Contains("- Подготовить смету\n  - Согласовать с бухгалтерией\n- Отправить клиенту", markdown, StringComparison.Ordinal);
        Assert.Contains("1. Первый шаг\n2. Второй шаг", markdown, StringComparison.Ordinal);
        Assert.Contains("| Статья | Сумма |\n| --- | --- |\n| Аренда | 1 200 |\n| Связь | 300 |", markdown, StringComparison.Ordinal);
        Assert.Contains("```\nvar итог = 1500;\n```", markdown, StringComparison.Ordinal);
        Assert.Contains("> Цитата руководителя", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Created_DocumentHasRealWordStyles()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read(Sample));

        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();
        Assert.Equal("Heading1", paragraphs[0].ParagraphProperties?.ParagraphStyleId?.Val?.Value);
        Assert.Equal("Отчёт за квартал", document.PackageProperties.Title);
        Assert.NotNull(document.MainDocumentPart.NumberingDefinitionsPart);
    }

    [Fact]
    public void SecondOrderedList_StartsFromItsOwnNumber()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read("1. a\n2. b\n\nТекст\n\n5. c\n6. d"));

        var items = DocxReader.Read(bytes).Blocks.Where(block => block.Kind == DocumentBlockKind.ListItem).Select(block => block.Number).ToList();

        Assert.Equal([1, 2, 5, 6], items);
    }

    // The formatting of the run where the match starts is kept: the model changes text, not the document's look.
    [Fact]
    public void Replace_KeepsTheFormattingOfTheRun()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read("Итог: **сумма 100 рублей** за месяц."));

        var (changed, count) = DocxEditor.Replace(bytes, "100", "250", all: false);

        Assert.Equal(1, count);
        var runs = DocxReader.Read(changed).Blocks.Single().Runs;
        Assert.Contains(runs, run => run.Text == "сумма 250 рублей" && run.Has(TextStyle.Bold));
        Assert.Contains(runs, run => run.Text == "Итог: " && !run.Has(TextStyle.Bold));
    }

    [Fact]
    public void Replace_FindsTextSplitAcrossRuns()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read("Привет, **дорогой** мир!"));

        var (changed, _) = DocxEditor.Replace(bytes, "Привет, дорогой мир", "Здравствуй,\nмир", all: false);

        var document = DocxReader.Read(changed);
        Assert.Equal("Здравствуй,\nмир!", document.Blocks.Single().PlainText);
    }

    [Fact]
    public void Replace_TextThatIsNotThere_IsAnErrorForTheModel()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read("Один абзац."));

        var error = Assert.Throws<AgentToolException>(() => DocxEditor.Replace(bytes, "Два", "Три", all: false));

        Assert.Contains("Два", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Replace_SeveralOccurrences_NeedsAll()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read("кот и кот\n\nещё кот"));

        Assert.Throws<AgentToolException>(() => DocxEditor.Replace(bytes, "кот", "пёс", all: false));
        var (changed, count) = DocxEditor.Replace(bytes, "кот", "пёс", all: true);

        Assert.Equal(3, count);
        Assert.DoesNotContain("кот", MarkdownWriter.Write(DocxReader.Read(changed)), StringComparison.Ordinal);
    }

    [Fact]
    public void Insert_AfterTheParagraph_KeepsTheOrder()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read("# Заголовок\n\nПервый абзац.\n\nПоследний абзац."));

        var changed = DocxEditor.Insert(bytes, MarkdownReader.Read("Вставка\n\n| А | Б |\n|---|---|\n| 1 | 2 |"), "Первый абзац");

        var texts = DocxReader.Read(changed).Blocks.Select(block => block.Kind == DocumentBlockKind.Table ? "table" : block.PlainText).ToList();
        Assert.Equal(["Заголовок", "Первый абзац.", "Вставка", "table", "Последний абзац."], texts);
    }

    [Fact]
    public void Insert_WithoutAnchor_AppendsBeforeSectionProperties()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read("Текст."));

        var changed = DocxEditor.Insert(bytes, MarkdownReader.Read("## Итоги"), after: null);

        using var document = WordprocessingDocument.Open(new MemoryStream(changed), false);
        var body = document.MainDocumentPart!.Document!.Body!;
        Assert.IsType<SectionProperties>(body.LastChild);
        Assert.Equal(2, DocxReader.Read(changed).Blocks.Single(block => block.Kind == DocumentBlockKind.Heading).Level);
    }

    // A non-Word document without styles or numbering: inserting adds them, so the heading and list read back as such.
    [Fact]
    public void Insert_IntoDocumentWithoutStyles_AddsThem()
    {
        var bytes = TestDocuments.BareWord("Старый текст");

        var changed = DocxEditor.Insert(bytes, MarkdownReader.Read("# Новый раздел\n\n- пункт"), "Старый");

        var blocks = DocxReader.Read(changed).Blocks;
        Assert.Equal((DocumentBlockKind.Heading, 1), (blocks[1].Kind, blocks[1].Level));
        Assert.Equal(DocumentBlockKind.ListItem, blocks[2].Kind);
    }

    [Fact]
    public void Insert_WithoutLists_LeavesNumberingAlone()
    {
        var changed = DocxEditor.Insert(TestDocuments.BareWord("Текст"), MarkdownReader.Read("Просто абзац"), "Текст");

        using var document = WordprocessingDocument.Open(new MemoryStream(changed), false);
        Assert.Null(document.MainDocumentPart!.NumberingDefinitionsPart);
    }

    [Fact]
    public void Reader_ShowsFieldResults_SkipsDeletedText_ReadsLinks()
    {
        var bytes = TestDocuments.WordWithFieldsAndRevisions();

        var markdown = MarkdownWriter.Write(DocxReader.Read(bytes));

        Assert.Contains("Страница 7 из отчёта", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("PAGE", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("удалено", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("перенесено", markdown, StringComparison.Ordinal);
        Assert.Contains("[ссылка](https://example.com/)", markdown, StringComparison.Ordinal);
    }

    // Word shows text moved under tracked changes at its new place; the model neither sees nor edits the old copy.
    [Fact]
    public void Replace_SkipsTextMovedAwayInTrackedChanges()
    {
        var bytes = TestDocuments.WordWithFieldsAndRevisions();

        Assert.Throws<AgentToolException>(() => DocxEditor.Replace(bytes, "перенесено", "x", all: false));
        var (changed, count) = DocxEditor.Replace(bytes, "из отчёта", "из сводки", all: false);

        Assert.Equal(1, count);
        Assert.Contains("Страница 7 из сводки", MarkdownWriter.Write(DocxReader.Read(changed)), StringComparison.Ordinal);
    }

    // Russian Word: styles are recognized by English names; an item without its own list takes it from the style.
    [Fact]
    public void Reader_UnderstandsADocumentFromRussianWord()
    {
        var markdown = MarkdownWriter.Write(DocxReader.Read(TestWordDocuments.RussianWord()));

        Assert.Equal(
            "# Глава 1\n\nОбычный **важный** текст.\n\nВ элементе управления\n\n- Пункт\n  - Подпункт\n\n| Шапка |  |\n| --- | --- |\n| Слева | Справа |\n|  | Ещё |\n",
            markdown);
    }

    // As in Word, instances of one list continue the count and startOverride restarts it.
    [Fact]
    public void Reader_NumbersListsLikeWord()
    {
        var blocks = DocxReader.Read(TestWordDocuments.SharedNumbering()).Blocks;

        Assert.Equal([1, 2, 3, 1], blocks.Select(block => block.Number));
    }

    [Fact]
    public void NotAWordFile_IsInvalidData() =>
        Assert.Throws<InvalidDataException>(() => DocxReader.Read("не документ"u8.ToArray()));
}
