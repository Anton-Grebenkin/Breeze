using System.IO;
using System.Windows;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.Wpf.Input;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Files dropped on the editor area outside a tab group, such as the welcome page, open in the group under the cursor
/// or the active one. Drops on a group are handled by <see cref="EditorGroupView"/>.
/// </summary>
public sealed partial class WorkbenchView
{
    private static bool IsEditorArea(object sender) => sender is FrameworkElement { Tag: nameof(ToolWindowLocation.Editor) };

    private static void OnFileDragOver(object sender, DragEventArgs e)
    {
        if (IsEditorArea(sender) && FileDragData.HasPaths(e.Data))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OpenDroppedFiles(object sender, DragEventArgs e)
    {
        if (!IsEditorArea(sender) || Main is not { } main || FileDragData.GetPaths(e.Data) is not { } paths)
        {
            return;
        }

        e.Handled = true;
        _ = OpenFilesAsync(main.EditorArea.Editors, GroupUnder(e.OriginalSource), [.. paths.Where(File.Exists)]);
    }

    private static async Task OpenFilesAsync(EditorAreaViewModel editors, EditorGroupViewModel? group, IReadOnlyList<string> files)
    {
        foreach (var file in files)
        {
            if (group is not null)
            {
                editors.Activate(group);
            }

            await editors.OpenAsync(new OpenFileRequest(file));
        }
    }
}
