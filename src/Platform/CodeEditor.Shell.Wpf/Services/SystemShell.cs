using System.Diagnostics;
using System.Windows;
using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.Wpf.Services;

/// <summary>
/// WPF clipboard, Windows Explorer (<c>explorer.exe /select,</c>), browser and default apps.
/// </summary>
public sealed class SystemShell : ISystemShell
{
    public void CopyToClipboard(string text) => Clipboard.SetText(text);

    public void RevealInFileManager(string path)
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add($"/select,{path}");
        using var _ = Process.Start(startInfo);
    }

    public void OpenInBrowser(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        // Web and mail addresses only: a link from a model reply must not launch programs.
        if (uri.Scheme is not ("http" or "https" or "mailto"))
        {
            throw new ArgumentException($"Scheme '{uri.Scheme}' is not opened in the browser.", nameof(uri));
        }

        using var _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    public void OpenWithDefaultApp(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
