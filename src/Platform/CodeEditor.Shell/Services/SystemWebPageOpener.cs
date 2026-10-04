namespace CodeEditor.Shell.Services;

/// <summary>Opens pages in the default browser; used when the Browser module isn't loaded.</summary>
public sealed class SystemWebPageOpener(ISystemShell shell) : IWebPageOpener
{
    public Task OpenAsync(Uri url)
    {
        shell.OpenInBrowser(url);
        return Task.CompletedTask;
    }
}
