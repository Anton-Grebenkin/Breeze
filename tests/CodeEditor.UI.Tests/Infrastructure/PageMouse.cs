using System.Text.Json;
using CodeEditor.Modules.Browser.Services;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// A mouse press inside a page through DevTools, for engine tests: it is not the user's mouse, so nothing outside the
/// page is touched and no window needs to be in front.
/// </summary>
internal static class PageMouse
{
    /// <param name="reference">The element's ref from the page's last snapshot.</param>
    public static async Task PressAsync(IBrowserPage page, string reference, CancellationToken cancellationToken)
    {
        using var point = JsonDocument.Parse(await page.EvaluateAsync(BrowserScripts.Point(reference), cancellationToken));
        foreach (var mouse in BrowserPointer.Click(point.RootElement.GetProperty("x").GetDouble(), point.RootElement.GetProperty("y").GetDouble()))
        {
            await page.DevToolsAsync(BrowserPointer.Method, mouse, cancellationToken);
        }
    }
}
