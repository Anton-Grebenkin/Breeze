namespace CodeEditor.Shell.Services;

/// <summary>
/// Opens a web page for the user: in the built-in browser tab next to files if the Browser module is loaded
/// (ADR 0027, ADR 0031), otherwise with the default app. Modules open addresses (container ports, previews) through it
/// without depending on the Browser module. Call on the UI thread.
/// </summary>
public interface IWebPageOpener
{
    /// <returns>Completes when the page is open or the error has been shown where it was opened.</returns>
    Task OpenAsync(Uri url);
}
