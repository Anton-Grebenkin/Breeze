using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Browser tool actions beyond navigation and the snapshot (<see cref="BrowserAgentTools"/>), as in Playwright MCP:
/// pressing keys, scrolling, waiting for text, running a script and reading the page's network requests and responses.
/// </summary>
public sealed class BrowserPageActions(IBrowserEngine browser, BrowserNetworkLog network)
{
    public const int DefaultWaitSeconds = 10;
    public const int MaxWaitSeconds = 60;

    /// <summary>Requests shown by <c>network</c>; static files (images, fonts, styles) only if they failed.</summary>
    public const int MaxRequests = 50;

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);
    private static readonly string[] StaticTypes = ["Image", "Font", "Stylesheet", "Media", "Manifest", "Other"];

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
        var shown = network.Recent(BrowserNetworkLog.Capacity)
            .Where(static request => !StaticTypes.Contains(request.Type) || request.Error is not null || request.Status >= 400)
            .TakeLast(MaxRequests)
            .Select(BrowserNetworkLog.Describe)
            .ToList();
        return shown.Count == 0 ? Strings.NetworkEmpty : string.Join('\n', shown);
    }

    /// <summary>The body of a request's response, as text.</summary>
    public async Task<string> ResponseAsync(string id, CancellationToken cancellationToken)
    {
        var request = network.Find(id) ?? throw new AgentToolException(Format(Strings.NoSuchRequest, id));
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
