using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Resources;

namespace CodeEditor.Shell.Palette;

/// <summary>The ":" mode: go to a line in the active file (<c>Ctrl+G</c>), as in VS Code.</summary>
public sealed class GoToLineQuickOpenProvider(EditorAreaViewModel editors, ICommandService commands) : IQuickOpenProvider
{
    public const string GoToLinePrefix = ":";

    public string Prefix => GoToLinePrefix;

    public string Placeholder => Strings.LineNumberPlaceholder;

    public string EmptyText => editors.Active is null ? Strings.OpenFileToGoToLine : Strings.TypeLineNumber;

    public void Prepare()
    {
    }

    public IReadOnlyList<PaletteItem> Filter(string text) =>
        editors.Active is { } tab && int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var line) && line > 0
            ? [new PaletteItem(line.ToString(CultureInfo.InvariantCulture), string.Format(CultureInfo.CurrentCulture, Strings.GoToLineNumber, line), null, [], false) { Detail = tab.Title }]
            : [];

    public async Task AcceptAsync(PaletteItem item, string text) =>
        await commands.ExecuteAsync(ShellCommandIds.EditorGoToLine, int.Parse(item.Id, CultureInfo.InvariantCulture));
}
