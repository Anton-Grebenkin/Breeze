using System.ComponentModel;
using CodeEditor.Shell.Services;

namespace CodeEditor.Testing;

public sealed class FakeSystemShell : ISystemShell
{
    public string? Clipboard { get; private set; }

    public string? Revealed { get; private set; }

    public Uri? Opened { get; private set; }

    public void CopyToClipboard(string text) => Clipboard = text;

    public void RevealInFileManager(string path) => Revealed = path;

    public void OpenInBrowser(Uri uri) => Opened = uri;

    /// <summary>Files opened with the default app.</summary>
    public List<string> Launched { get; } = [];

    /// <summary>No app is associated with the file: <see cref="OpenWithDefaultApp"/> throws, as Windows does.</summary>
    public bool LaunchFails { get; set; }

    public void OpenWithDefaultApp(string path)
    {
        Launched.Add(path);
        if (LaunchFails)
        {
            throw new Win32Exception(2);
        }
    }
}
