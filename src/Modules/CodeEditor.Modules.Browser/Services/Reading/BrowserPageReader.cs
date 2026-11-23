using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Services.Reading;

/// <summary>
/// What the <c>browser</c> tool tells the model about a page: the snapshot or the main text as Markdown, a long one in
/// parts (<see cref="PageParts"/>), where a text occurs on the page, and a screenshot of one element. Page text is
/// framed in a tag that names the tab, the address and the part.
/// </summary>
public static class BrowserPageReader
{
    public static async Task<string> SnapshotAsync(BrowserTab tab, int? part, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tab);
        return Frame("browser_page", tab, await tab.Page.EvaluateAsync(BrowserSnapshot.Script, cancellationToken), part, BrowserAgentTools.Snapshot, Strings.SnapshotTruncated);
    }

    public static async Task<string> TextAsync(BrowserTab tab, int? part, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tab);
        return Frame("browser_text", tab, await tab.Page.EvaluateAsync(BrowserReading.Markdown, cancellationToken), part, BrowserAgentTools.Read, Strings.SnapshotTruncated);
    }

    public static async Task<string> FindAsync(BrowserTab tab, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tab);
        using var reply = JsonDocument.Parse(await tab.Page.EvaluateAsync(BrowserReading.Find(text), cancellationToken));
        var total = reply.RootElement.TryGetProperty("total", out var count) ? count.GetInt32() : 0;
        if (total == 0)
        {
            return Format(Strings.FindNone, text);
        }

        var places = reply.RootElement.GetProperty("places");
        var lines = new StringBuilder(Format(Strings.FindSummary, text, total, places.GetArrayLength()));
        var index = 0;
        foreach (var place in places.EnumerateArray())
        {
            lines.Append('\n').Append(CultureInfo.InvariantCulture, $"{++index}.");
            if (Text(place, "heading") is { Length: > 0 } heading)
            {
                lines.Append(' ').Append(Format(Strings.FindUnder, heading));
            }

            if (Text(place, "ref") is { Length: > 0 } reference)
            {
                lines.Append(CultureInfo.InvariantCulture, $" [ref={reference}]");
            }

            if (place.TryGetProperty("count", out var times) && times.GetInt32() > 1)
            {
                lines.Append(CultureInfo.InvariantCulture, $" ×{times.GetInt32()}");
            }

            lines.Append(": ").Append(Text(place, "context"));
        }

        return places.GetArrayLength() >= BrowserReading.MaxPlaces ? lines.Append('\n').Append(Strings.FindMore).ToString() : lines.ToString();
    }

    /// <summary>A screenshot of one snapshot element (a chart, a canvas), scrolled into view.</summary>
    /// <exception cref="AgentToolException">The element is not on the page.</exception>
    public static async Task<byte[]> CaptureElementAsync(BrowserTab tab, string reference, BrowserImageFormat format, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var box = await tab.Page.EvaluateAsync(BrowserScripts.Box(reference), cancellationToken);
        var request = BrowserScreenshot.ElementRequest(box, format) ?? throw new AgentToolException(Strings.ElementNotFound);
        return BrowserScreenshot.Data(await tab.Page.DevToolsAsync("Page.captureScreenshot", request, cancellationToken));
    }

    private static string Frame(string tag, BrowserTab tab, string json, int? part, string action, string truncatedNote)
    {
        using var reply = JsonDocument.Parse(json);
        var content = reply.RootElement;
        var (body, number, count) = PageParts.Take(Text(content, "text"), part);
        var status = tab.Page.StatusCode is >= 400 and var code ? string.Create(CultureInfo.InvariantCulture, $" status=\"{code}\"") : string.Empty;
        var parts = count > 1 ? string.Create(CultureInfo.InvariantCulture, $" part=\"{number}/{count}\"") : string.Empty;
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"<{tag} tab=\"{tab.Id}\" url=\"{WebUtility.HtmlEncode(Text(content, "url"))}\" title=\"{WebUtility.HtmlEncode(Text(content, "title"))}\"{status}{parts}>\n")
            .Append(body.Length > 0 ? body : Strings.EmptyPage);
        if (number < count)
        {
            text.Append('\n').Append(Format(Strings.PartNote, number, count, action, number + 1));
        }
        else if (content.TryGetProperty("truncated", out var truncated) && truncated.ValueKind == JsonValueKind.True)
        {
            text.Append('\n').Append(truncatedNote);
        }

        return text.Append(CultureInfo.InvariantCulture, $"\n</{tag}>").ToString();
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
