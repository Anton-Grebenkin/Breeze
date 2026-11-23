using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Browser tool actions beyond navigation and the snapshot (<see cref="BrowserAgentTools"/>), as in Playwright MCP:
/// clicking, hovering and dragging with real mouse events, choosing a file for a file field, pressing keys, scrolling,
/// waiting for text, running a script and reading the page's network requests and responses.
/// Made for the page a call works on.
/// </summary>
public sealed class BrowserPageActions(IBrowserPage browser)
{
    public const int DefaultWaitSeconds = 10;
    public const int MaxWaitSeconds = 60;

    /// <summary>Requests shown by <c>network</c>; static files (images, fonts, styles) only if they failed.</summary>
    public const int MaxRequests = 50;

    /// <summary>How long to wait for navigation after a click or typing.</summary>
    public static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(10);
    /// <summary>A response body longer than this is cut: the output store keeps the rest of what fits.</summary>
    public const int MaxResponseCharacters = 400_000;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);
    private static readonly string[] RequestMethods = ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE"];
    private static readonly string[] StaticTypes = ["Image", "Font", "Stylesheet", "Media", "Manifest", "Other"];

    /// <summary>
    /// Clicks a snapshot element as a person would: the mouse moves there, presses and releases, so hover styles,
    /// pointer handlers and trusted-event checks work. If the click did not reach the element — something covers it, or
    /// the page is behind another tab and takes no input — the element is clicked by script, as before.
    /// </summary>
    public async Task ClickAsync(string reference, CancellationToken cancellationToken)
    {
        var address = browser.Url;
        var point = await PointAsync(reference, cancellationToken);
        if (point.Kind == "file")
        {
            throw new AgentToolException(Strings.UseUpload);
        }

        if (point.Kind != "select")
        {
            foreach (var mouse in BrowserPointer.Click(point.X, point.Y))
            {
                await browser.DevToolsAsync(BrowserPointer.Method, mouse, cancellationToken);
            }
        }

        // A click that took the page elsewhere cannot be asked about: the probe lived in the page that is gone.
        if (browser.IsLoading || browser.Url != address || await browser.EvaluateAsync(BrowserScripts.WasClicked(reference), cancellationToken) == "true")
        {
            return;
        }

        // Not found now means the real click did take the page elsewhere.
        await browser.EvaluateAsync(BrowserScripts.Click(reference), cancellationToken);
    }

    /// <summary>
    /// Types into a snapshot field, replacing the value; a list picks an option by text; <paramref name="submit"/>
    /// presses Enter. Typing may start navigation, so the load is awaited.
    /// </summary>
    public async Task TypeAsync(string reference, string text, bool submit, CancellationToken cancellationToken)
    {
        using var result = JsonDocument.Parse(await browser.EvaluateAsync(BrowserScripts.Type(reference, text, submit), cancellationToken));
        if (result.RootElement.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
        {
            var error = result.RootElement.TryGetProperty("error", out var reason) ? reason.GetString() : null;
            throw new AgentToolException(error == "no option" ? Strings.NoSuchOption : Strings.ElementNotFound);
        }

        await browser.WaitForLoadAsync(SettleTimeout, cancellationToken);
    }

    /// <summary>Moves the mouse over a snapshot element: menus and hints that open on hover appear.</summary>
    public async Task HoverAsync(string reference, CancellationToken cancellationToken)
    {
        var point = await PointAsync(reference, cancellationToken);
        await browser.DevToolsAsync(BrowserPointer.Method, BrowserPointer.Move(point.X, point.Y), cancellationToken);
    }

    /// <summary>
    /// Drags a snapshot element onto another: with the mouse held, or — for an element marked draggable — with the
    /// page's own drag-and-drop events.
    /// </summary>
    public async Task DragAsync(string reference, string target, CancellationToken cancellationToken)
    {
        var from = await PointAsync(reference, cancellationToken);
        if (from.Draggable)
        {
            await RunAsync(BrowserScripts.DragDrop(reference, target), cancellationToken);
            return;
        }

        var to = await PointAsync(target, cancellationToken);
        from = await PointAsync(reference, cancellationToken);
        foreach (var mouse in BrowserPointer.Drag(from.X, from.Y, to.X, to.Y))
        {
            await browser.DevToolsAsync(BrowserPointer.Method, mouse, cancellationToken);
        }
    }

    /// <summary>Puts a file into a file field, without the system dialog; the page gets its change event.</summary>
    public async Task UploadAsync(string reference, string fullPath, CancellationToken cancellationToken)
    {
        var found = new JsonObject { ["expression"] = BrowserScripts.FileInput(reference), ["returnByValue"] = false }.ToJsonString();
        using var element = JsonDocument.Parse(await browser.DevToolsAsync("Runtime.evaluate", found, cancellationToken));
        if (!element.RootElement.TryGetProperty("result", out var result) || !result.TryGetProperty("objectId", out var id))
        {
            throw new AgentToolException(Strings.NotFileField);
        }

        var files = new JsonObject { ["files"] = new JsonArray(fullPath), ["objectId"] = id.GetString() }.ToJsonString();
        await browser.DevToolsAsync("DOM.setFileInputFiles", files, cancellationToken);
    }

    /// <summary>
    /// A request to the page's own site with its sign-in (cookies), like the page's own scripts make: the site's API
    /// is read or called without clicking through its pages. The reply comes framed, with the status and the type.
    /// </summary>
    /// <exception cref="AgentToolException">The method is unknown, the address is of another site, or the request failed.</exception>
    public async Task<string> RequestAsync(string tabId, string? method, string address, string? body, CancellationToken cancellationToken)
    {
        var verb = Verb(method);
        using var reply = JsonDocument.Parse(await EvaluateAsync(BrowserScripts.Request(verb, address, body, MaxResponseCharacters), cancellationToken));
        var answer = reply.RootElement;
        if (answer.ValueKind != JsonValueKind.Object || !answer.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            var error = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("error", out var reason) ? reason.GetString() : null;
            throw new AgentToolException(error == "other site" ? Strings.RequestOtherSite : Format(Strings.RequestFailed, error ?? "?"));
        }

        var cut = answer.TryGetProperty("cut", out var isCut) && isCut.GetBoolean() ? "\n" + Strings.SnapshotTruncated : string.Empty;
        return string.Create(CultureInfo.InvariantCulture,
            $"<browser_response tab=\"{tabId}\" method=\"{verb}\" status=\"{answer.GetProperty("status").GetInt32()}\" type=\"{WebUtility.HtmlEncode(answer.GetProperty("type").GetString())}\" url=\"{WebUtility.HtmlEncode(answer.GetProperty("url").GetString())}\">\n{answer.GetProperty("body").GetString()}{cut}\n</browser_response>");
    }

    /// <summary>The HTTP method of a request, upper case; GET when none is given.</summary>
    /// <exception cref="AgentToolException">Not one of the methods a request may use.</exception>
    public static string Verb(string? method)
    {
        var verb = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
        return RequestMethods.Contains(verb) ? verb : throw new AgentToolException(Format(Strings.RequestBadMethod, method!, string.Join(", ", RequestMethods)));
    }

    /// <summary>The request only reads: it changes nothing on the site.</summary>
    public static bool IsReading(string? method) => string.IsNullOrWhiteSpace(method) || method.Trim().ToUpperInvariant() is "GET" or "HEAD";

    /// <summary>Presses a key on the focused element, or on <paramref name="reference"/> after focusing it.</summary>
    public async Task PressAsync(string key, string? reference, CancellationToken cancellationToken)
    {
        if (!BrowserKeys.IsKnown(key))
        {
            throw new AgentToolException(Format(Strings.UnknownKey, key, BrowserKeys.Names));
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            await RunAsync(BrowserScripts.Focus(reference.Trim()), cancellationToken);
        }

        foreach (var keyEvent in BrowserKeys.Press(key))
        {
            await browser.DevToolsAsync("Input.dispatchKeyEvent", keyEvent, cancellationToken);
        }

        await browser.WaitForLoadAsync(SettleTimeout, cancellationToken);
    }

    public Task ScrollAsync(string? reference, string? direction, CancellationToken cancellationToken) =>
        RunAsync(BrowserScripts.Scroll(string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(), direction), cancellationToken);

    /// <returns><c>false</c> if the text did not appear in time.</returns>
    public async Task<bool> WaitForTextAsync(string text, int? seconds, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(Math.Clamp(seconds ?? DefaultWaitSeconds, 1, MaxWaitSeconds));
        while (true)
        {
            if (await browser.EvaluateAsync(BrowserScripts.HasText(text), cancellationToken) == "true")
            {
                return true;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    /// <summary>Runs a JavaScript expression in the page, awaiting a promise; the value comes back as JSON.</summary>
    public async Task<string> EvaluateAsync(string script, CancellationToken cancellationToken)
    {
        var parameters = new JsonObject { ["expression"] = script, ["awaitPromise"] = true, ["returnByValue"] = true }.ToJsonString();
        using var reply = JsonDocument.Parse(await browser.DevToolsAsync("Runtime.evaluate", parameters, cancellationToken));
        var root = reply.RootElement;
        if (root.TryGetProperty("exceptionDetails", out var details))
        {
            var message = details.TryGetProperty("exception", out var exception) && exception.TryGetProperty("description", out var description)
                ? description.GetString()
                : details.TryGetProperty("text", out var text) ? text.GetString() : null;
            throw new AgentToolException(Format(Strings.ScriptFailed, message ?? "?"));
        }

        if (!root.TryGetProperty("result", out var result))
        {
            return "undefined";
        }

        return result.TryGetProperty("value", out var value) ? value.GetRawText()
            : result.TryGetProperty("description", out var shown) ? shown.GetString() ?? "undefined"
            : "undefined";
    }

    /// <summary>The page's latest requests, oldest first; static files only if they failed.</summary>
    public string Requests()
    {
        var shown = browser.Network.Recent(BrowserNetworkLog.Capacity)
            .Where(static request => !StaticTypes.Contains(request.Type) || request.Error is not null || request.Status >= 400)
            .TakeLast(MaxRequests)
            .Select(BrowserNetworkLog.Describe)
            .ToList();
        return shown.Count == 0 ? Strings.NetworkEmpty : string.Join('\n', shown);
    }

    /// <summary>The body of a request's response, as text.</summary>
    public async Task<string> ResponseAsync(string id, CancellationToken cancellationToken)
    {
        var request = browser.Network.Find(id) ?? throw new AgentToolException(Format(Strings.NoSuchRequest, id));
        string reply;
        try
        {
            reply = await browser.DevToolsAsync("Network.getResponseBody", new JsonObject { ["requestId"] = id }.ToJsonString(), cancellationToken);
        }
        catch (BrowserException)
        {
            throw new AgentToolException(Format(Strings.NoResponseBody, id));
        }

        using var document = JsonDocument.Parse(reply);
        var body = document.RootElement.TryGetProperty("body", out var text) ? text.GetString() ?? string.Empty : string.Empty;
        var encoded = document.RootElement.TryGetProperty("base64Encoded", out var flag) && flag.GetBoolean();
        var header = BrowserNetworkLog.Describe(request) + (request.MimeType is { } mime ? " " + mime : string.Empty);
        return header + "\n" + (encoded ? Decode(body, request.MimeType) : body);
    }

    // Text sent as base64 (a compressed or binary-flagged response) is decoded; anything else is only sized.
    private static string Decode(string base64, string? mimeType)
    {
        var bytes = Convert.FromBase64String(base64);
        var isText = mimeType is { } mime && (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mime.Contains("json", StringComparison.OrdinalIgnoreCase) || mime.Contains("xml", StringComparison.OrdinalIgnoreCase));
        return isText ? Encoding.UTF8.GetString(bytes) : Format(Strings.BinaryBody, bytes.Length);
    }

    // Scrolled into view first: the second look at a drag's start accounts for the scroll its target caused.
    private async Task<(double X, double Y, string Kind, bool Draggable)> PointAsync(string reference, CancellationToken cancellationToken)
    {
        using var reply = JsonDocument.Parse(await browser.EvaluateAsync(BrowserScripts.Point(reference), cancellationToken));
        var point = reply.RootElement;
        return point.TryGetProperty("ok", out var ok) && ok.GetBoolean()
            ? (point.GetProperty("x").GetDouble(), point.GetProperty("y").GetDouble(), point.GetProperty("kind").GetString() ?? string.Empty, point.GetProperty("draggable").GetBoolean())
            : throw new AgentToolException(Strings.ElementNotFound);
    }

    private async Task RunAsync(string script, CancellationToken cancellationToken)
    {
        using var result = JsonDocument.Parse(await browser.EvaluateAsync(script, cancellationToken));
        if (result.RootElement.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
        {
            throw new AgentToolException(Strings.ElementNotFound);
        }
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
