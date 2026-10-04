using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>A1 addresses and ranges, page selections like "1,4-6", shifting and renaming formula references.</summary>
public sealed class AddressesAndSelectionsTests
{
    [Theory]
    [InlineData("A1", 1, 1)]
    [InlineData("$B$12", 12, 2)]
    [InlineData("z3", 3, 26)]
    [InlineData("AA7", 7, 27)]
    [InlineData("XFD1048576", 1_048_576, 16_384)]
    public void Address_Parses(string text, int row, int column)
    {
        Assert.True(CellAddress.TryParse(text, out var address));
        Assert.Equal(new CellAddress(row, column), address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A0")]
    [InlineData("1A")]
    [InlineData("XFE1")]
    [InlineData("ABCD1")]
    public void Address_RejectsGarbage(string text) => Assert.False(CellAddress.TryParse(text, out _));

    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(52, "AZ")]
    [InlineData(703, "AAA")]
    public void ColumnName_IsBijective(int column, string name)
    {
        Assert.Equal(name, CellAddress.ColumnName(column));
        Assert.Equal(column, CellAddress.ColumnNumber(name));
    }

    [Theory]
    [InlineData("A1:D20", "A1:D20")]
    [InlineData("D20:A1", "A1:D20")]
    [InlineData("B3", "B3")]
    [InlineData("B:D", "B1:D1048576")]
    [InlineData("3:10", "A3:XFD10")]
    public void Range_Parses(string text, string expected)
    {
        Assert.True(CellRange.TryParse(text, out var range));
        Assert.Equal(expected, range.ToString());
    }

    [Theory]
    [InlineData(null, 3, new[] { 1, 2, 3 })]
    [InlineData("2", 5, new[] { 2 })]
    [InlineData("2-4", 5, new[] { 2, 3, 4 })]
    [InlineData("4-", 5, new[] { 4, 5 })]
    [InlineData("1, 3–4, 3", 5, new[] { 1, 3, 4 })]
    public void Pages_Parse(string? text, int count, int[] expected)
    {
        Assert.True(PageSelection.TryParse(text, count, out var pages));
        Assert.Equal(expected, pages);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("3-2")]
    [InlineData("a")]
    public void Pages_OutsideTheDocument_AreRefused(string text) => Assert.False(PageSelection.TryParse(text, 5, out _));

    [Fact]
    public void Pages_AreDescribedAsRanges() => Assert.Equal("1–3, 7", PageSelection.Describe([1, 2, 3, 7]));

    [Theory]
    [InlineData("A1*2", 1, 0, "A2*2")]
    [InlineData("$A$1+B1", 2, 1, "$A$1+C3")]
    [InlineData("SUM(A1:A3)+LOG10(B1)", 1, 0, "SUM(A2:A4)+LOG10(B2)")]
    [InlineData("\"A1\"&A1", 1, 0, "\"A1\"&A2")]
    [InlineData("'Q1 data'!A1+Лист1!B2", 1, 0, "'Q1 data'!A2+Лист1!B3")]
    [InlineData("A1", -1, 0, "#REF!")]
    [InlineData("SUM(Table1[[#This Row],[Q1]])+A1", 1, 0, "SUM(Table1[[#This Row],[Q1]])+A2")]
    [InlineData("SUM(Data!B:B)+SUM($B:B)", 0, 1, "SUM(Data!C:C)+SUM($B:C)")]
    [InlineData("SUM(3:3)+SUM($3:4)", 2, 0, "SUM(5:5)+SUM($3:6)")]
    [InlineData("[1]Sheet1!A1", 1, 0, "[1]Sheet1!A2")]
    [InlineData("Table1[Col'[1']]*B2", 1, 1, "Table1[Col'[1']]*C3")]
    public void Formula_ShiftsRelativeReferences(string formula, int rows, int columns, string expected) =>
        Assert.Equal(expected, FormulaReferences.Shift(formula, rows, columns));

    [Theory]
    [InlineData("SUM(Data!A1:A3)", "SUM('Q1 data'!A1:A3)")]
    [InlineData("'Data'!B2*2", "'Q1 data'!B2*2")]
    [InlineData("DataX!A1+\"Data!A1\"", "DataX!A1+\"Data!A1\"")]
    [InlineData("[1]Data!A1+Data!A1", "[1]Data!A1+'Q1 data'!A1")]
    public void Formula_FollowsTheRenamedSheet(string formula, string expected) =>
        Assert.Equal(expected, FormulaReferences.RenameSheet(formula, "Data", "Q1 data"));
}
