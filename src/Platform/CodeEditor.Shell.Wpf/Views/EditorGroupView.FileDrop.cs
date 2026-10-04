using System.IO;
using System.Windows;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Wpf.Input;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Files in tab drags and file drops: a dragged tab also carries its file (<see cref="FileDragData"/>), so it can be
/// dropped into the agent chat; files from the explorer or Windows dropped on the content open in this group, or in
/// a new group at an edge, like a dragged tab; dropped on the tab strip they open in this group.
/// </summary>
public sealed partial class EditorGroupView
{
    // Moving a tab stays Move; Copy lets the chat attach the tab's file.
    private static void StartTabDrag(DependencyObject source, EditorTab tab)
    {
        var data = new DataObject(TabFormat, tab);
        if (tab.FilePath is { } path)
        {
            FileDragData.SetPaths(data, [path]);
        }

        DragDrop.DoDragDrop(source, data, DragDropEffects.Move | DragDropEffects.Copy);
    }

    private static DragDropEffects FileDropEffect(DragEventArgs e) =>
        FileDragData.HasPaths(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnFileDragOver(DragEventArgs e)
    {
        e.Effects = FileDropEffect(e);
        if (e.Effects == DragDropEffects.None)
        {
            HideHint();
            return;
        }

        ShowHint(ZoneAt(e.GetPosition(EditorContent)));
        e.Handled = true;
    }

    private void OpenDroppedFiles(DragEventArgs e, DropZone zone)
    {
        if (Area is not { } area || Group is not { } group || FileDragData.GetPaths(e.Data) is not { } paths)
        {
            return;
        }

        // Folders are skipped: the editor opens files.
        var files = paths.Where(File.Exists).ToArray();
        if (files.Length == 0)
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        var target = zone == DropZone.Center ? group : area.AddGroup(group, before: zone == DropZone.Left) ?? group;
        _ = OpenFilesAsync(area, target, files);
    }

    private static async Task OpenFilesAsync(EditorAreaViewModel area, EditorGroupViewModel group, IReadOnlyList<string> files)
    {
        foreach (var file in files)
        {
            area.Activate(group);
            await area.OpenAsync(new OpenFileRequest(file));
        }

        // A new edge group stays empty if no file could be opened.
        area.CloseEmptyGroups();
    }
}
