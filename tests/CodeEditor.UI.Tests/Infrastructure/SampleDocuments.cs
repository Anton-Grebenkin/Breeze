using System.IO.Compression;
using System.Text;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Small real documents for viewer UI tests: Word and Excel as minimal Open XML packages, PDF as one hand-built page
/// with text and a cross-reference table.
/// </summary>
public static class SampleDocuments
{
    private const string Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string OfficeDocument = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";

    public static void WriteDocx(string path, string paragraph) => WritePackage(path, new Dictionary<string, string>
    {
        ["[Content_Types].xml"] = ContentTypes(("/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")),
        ["_rels/.rels"] = Rels(("rId1", OfficeDocument, "word/document.xml")),
        ["word/document.xml"] =
            $"""<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>{paragraph}</w:t></w:r></w:p></w:body></w:document>""",
    });

    /// <summary>A workbook with one sheet; each row is string cells in column order.</summary>
    public static void WriteXlsx(string path, string sheet, params string[][] rows) => WritePackage(path, new Dictionary<string, string>
    {
        ["[Content_Types].xml"] = ContentTypes(
            ("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
            ("/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
        ["_rels/.rels"] = Rels(("rId1", OfficeDocument, "xl/workbook.xml")),
        ["xl/workbook.xml"] =
            $"""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{sheet}" sheetId="1" r:id="rId1"/></sheets></workbook>""",
        ["xl/_rels/workbook.xml.rels"] = Rels(("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml")),
        ["xl/worksheets/sheet1.xml"] =
            $"""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{string.Concat(rows.Select(Row))}</sheetData></worksheet>""",
    });

    /// <summary>A one-page PDF with Helvetica text; cross-reference offsets are computed in bytes.</summary>
    public static void WritePdf(string path, string text)
    {
        var content = $"BT /F1 24 Tf 40 70 Td ({text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var table = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        offsets.ForEach(offset => pdf.Append($"{offset:D10} 00000 n \n"));
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{table}\n%%EOF\n");
        File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
    }

    private static string Row(string[] cells, int row) =>
        $"""<row r="{row + 1}">{string.Concat(cells.Select((value, column) => $"""<c r="{(char)('A' + column)}{row + 1}" t="inlineStr"><is><t>{value}</t></is></c>"""))}</row>""";

    private static string ContentTypes(params (string Part, string Type)[] overrides) =>
        $"""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/>{string.Concat(overrides.Select(entry => $"""<Override PartName="{entry.Part}" ContentType="{entry.Type}"/>"""))}</Types>""";

    private static string Rels(params (string Id, string Type, string Target)[] relationships) =>
        $"""<Relationships xmlns="{Relationships}">{string.Concat(relationships.Select(entry => $"""<Relationship Id="{entry.Id}" Type="{entry.Type}" Target="{entry.Target}"/>"""))}</Relationships>""";

    private static void WritePackage(string path, Dictionary<string, string> parts)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, xml) in parts)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + xml);
        }
    }
}
