using System.Windows;
using CodeEditor.Shell.Services;
using Microsoft.Win32;

namespace CodeEditor.Shell.Wpf.Services;

/// <summary>Windows system dialogs: <see cref="OpenFolderDialog"/> and <see cref="OpenFileDialog"/>.</summary>
public sealed class FileDialogs(Application application) : IFileDialogs
{
    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return dialog.ShowDialog(application.MainWindow) == true ? dialog.FolderName : null;
    }

    public string? PickFile(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Multiselect = false, CheckFileExists = true };
        return dialog.ShowDialog(application.MainWindow) == true ? dialog.FileName : null;
    }
}
