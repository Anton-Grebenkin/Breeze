using System.Text.Json;
using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Formats.Word;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Created and edited files pass Open XML SDK schema validation (element order, required attributes, relationships);
/// otherwise Word or Excel would offer to repair the file on open.
/// </summary>
public sealed class OpenXmlValidityTests
{
    private const string Markdown = """
        # Заголовок

        Абзац с **жирным**, *курсивом*, `кодом` и [ссылкой](https://example.com).

        - пункт
          - вложенный
        1. первый
        2. второй

        | А | Б |
        | --- | --- |
        | 1 | 2 |

        ```
        код
        ```

        > цитата

        ---
        """;

    [Fact]
    public void CreatedWord_IsValid() => AssertValidWord(DocxWriter.Create(MarkdownReader.Read(Markdown)));

    [Fact]
    public void EditedWord_IsValid()
    {
        var bytes = DocxWriter.Create(MarkdownReader.Read(Markdown));

        var replaced = DocxEditor.Replace(bytes, "жирным", "очень\nжирным", all: false).Bytes;
        var inserted = DocxEditor.Insert(replaced, MarkdownReader.Read(Markdown), "Абзац");

        AssertValidWord(inserted);
    }

    [Fact]
    public void InsertIntoBareWord_IsValid() =>
        AssertValidWord(DocxEditor.Insert(TestDocuments.BareWord("Текст"), MarkdownReader.Read(Markdown), "Текст"));

    // New lists go between the existing num instances and numIdMacAtCleanup, which the schema requires to be last.
    [Fact]
    public void InsertListsIntoWordFromMac_IsValid() =>
        AssertValidWord(DocxEditor.Insert(TestWordDocuments.SharedNumbering(), MarkdownReader.Read(Markdown), "второй"));

    [Fact]
    public void CreatedWorkbook_IsValid() => AssertValidExcel(XlsxWriter.Create([Sheet("Данные"), Sheet("Итоги")]));

    [Fact]
    public void EditedWorkbook_IsValid()
    {
        var bytes = XlsxEditor.SetCells(TestDocuments.ExcelLikeWorkbook(), null, [(new CellAddress(2, 2), Value("\"7\"")), (new CellAddress(20, 30), Value("\"=A1\""))]);
        bytes = XlsxEditor.AddSheet(bytes, Sheet("Новый"));
        bytes = XlsxEditor.RenameSheet(bytes, "Данные", "Продажи");

        AssertValidExcel(bytes);
    }

    [Fact]
    public void EditedTableHeader_IsValid() =>
        AssertValidExcel(XlsxEditor.SetCells(TestDocuments.WorkbookWithHiddenSheetAndTable(), "Продажи", [(new CellAddress(1, 2), Value("\"Итого\"")), (new CellAddress(4, 1), Value("\"Олег\""))]));

    private static SheetInput Sheet(string name) => new(name, [[Value("\"Имя\""), Value("\"Сумма\"")], [Value("\"Иван\""), Value("\"=1+2\"")]]);

    private static JsonElement Value(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static void AssertValidWord(byte[] bytes)
    {
        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        AssertValid(document);
    }

    private static void AssertValidExcel(byte[] bytes)
    {
        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        AssertValid(document);
    }

    private static void AssertValid(OpenXmlPackage package)
    {
        var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(package)
            .Select(error => $"{error.Part?.Uri} {error.Path?.XPath}: {error.Description}")
            .ToList();

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
