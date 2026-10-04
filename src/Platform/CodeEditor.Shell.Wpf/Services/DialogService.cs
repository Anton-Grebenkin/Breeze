using System.Globalization;
using System.Windows;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Wpf.Views;

namespace CodeEditor.Shell.Wpf.Services;

/// <summary>
/// Confirmations via the system message box, as VS Code does for file deletion and closing with edits.
/// </summary>
public sealed class DialogService(Application application, ISystemShell systemShell) : IDialogService
{
    private const int MaxListedFiles = 10;

    public bool Confirm(string message, string detail, string confirmText) =>
        Show($"{message}\n\n{detail}", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;

    public void Inform(string message, string detail) =>
        Show($"{message}\n\n{detail}", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

    public SaveChoice AskToSave(IReadOnlyList<string> fileNames)
    {
        var message = fileNames.Count == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.SaveChangesToFile, fileNames[0])
            : string.Format(CultureInfo.CurrentCulture, Strings.SaveChangesToFiles, fileNames.Count, string.Join('\n', fileNames.Take(MaxListedFiles)));

        return Show($"{message}\n\n{Strings.UnsavedChangesLost}", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) switch
        {
            MessageBoxResult.Yes => SaveChoice.Save,
            MessageBoxResult.No => SaveChoice.DontSave,
            _ => SaveChoice.Cancel,
        };
    }

    public string? PromptSecret(string title, string message, Uri? link = null)
    {
        var window = new SecretPromptWindow(title, message, link, systemShell.OpenInBrowser);
        if (application.MainWindow is { IsVisible: true } owner)
        {
            window.Owner = owner;
        }

        return window.ShowDialog() == true ? window.Secret : null;
    }

    private MessageBoxResult Show(string text, MessageBoxButton buttons, MessageBoxImage image, MessageBoxResult defaultResult)
    {
        var caption = application.MainWindow?.Title ?? string.Empty;
        return application.MainWindow is { IsVisible: true } owner
            ? MessageBox.Show(owner, text, caption, buttons, image, defaultResult)
            : MessageBox.Show(text, caption, buttons, image, defaultResult);
    }
}
