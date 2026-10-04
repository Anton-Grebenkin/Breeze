using System.Globalization;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>
/// Excel workbook or CSV view: sheets as tabs at the bottom, the selected sheet as a grid. A huge sheet is read up to
/// <see cref="MaxRows"/> rows and <see cref="MaxColumns"/> columns so the viewer doesn't eat memory; an external app
/// opens the full file. The selected sheet survives a reload.
/// </summary>
public sealed partial class SpreadsheetViewerViewModel(string filePath, DocumentViewerContext context) : DocumentViewerViewModel(filePath, context)
{
    public const int MaxRows = 100_000;
    public const int MaxColumns = 500;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasManySheets))]
    public partial IReadOnlyList<SheetViewModel> Sheets { get; private set; } = [];

    [ObservableProperty]
    public partial SheetViewModel? SelectedSheet { get; set; }

    /// <summary>Sheet tabs are needed only with more than one sheet.</summary>
    public bool HasManySheets => Sheets.Count > 1;

    protected override object Read(CancellationToken cancellationToken)
    {
        var bytes = ReadBytes();
        cancellationToken.ThrowIfCancellationRequested();
        var book = Kind == DocumentKind.Csv ? CsvReader.Read(bytes, FilePath, MaxRows) : XlsxReader.Read(bytes, MaxRows);
        cancellationToken.ThrowIfCancellationRequested();
        return book.Sheets.Select(sheet => new SheetViewModel(sheet, CultureInfo.CurrentCulture, MaxColumns, MaxRows)).ToList();
    }

    protected override void Show(object content)
    {
        var previous = SelectedSheet?.Name;
        Sheets = (IReadOnlyList<SheetViewModel>)content;
        SelectedSheet = Sheets.FirstOrDefault(sheet => sheet.Name == previous) ?? Sheets.FirstOrDefault(sheet => !sheet.IsHidden) ?? Sheets.FirstOrDefault();
    }

    partial void OnSelectedSheetChanged(SheetViewModel? value) => Summary = value?.Summary ?? string.Empty;
}
