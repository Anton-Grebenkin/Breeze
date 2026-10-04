namespace CodeEditor.Shell.Services;

/// <summary>
/// Operating system actions: clipboard, Windows Explorer, browser and default apps for files.
/// </summary>
public interface ISystemShell
{
    void CopyToClipboard(string text);

    /// <summary>Opens Windows Explorer with the file or folder selected.</summary>
    void RevealInFileManager(string path);

    /// <summary>Opens an <c>http</c>, <c>https</c> or <c>mailto</c> address with the default app.</summary>
    void OpenInBrowser(Uri uri);

    /// <summary>Opens a file with its default app, like a double click in Explorer.</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">No app is associated or it failed to start.</exception>
    void OpenWithDefaultApp(string path);
}
