using System.Windows;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Services.Attachments;
using Microsoft.Win32;

namespace CodeEditor.Modules.Agent.Wpf.Services;

/// <summary>The Windows file dialog for message attachments, with multi-select.</summary>
public sealed class AttachmentPicker(Application application) : IAttachmentPicker
{
    public IReadOnlyList<string> PickFiles(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Multiselect = true, CheckFileExists = true, Filter = Strings.AttachFilesFilter };
        return dialog.ShowDialog(application.MainWindow) == true ? dialog.FileNames : [];
    }
}
