using System.Globalization;
using System.Text.Json;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// The open page's requests from DevTools <c>Network</c> events, the last <see cref="Capacity"/>: the agent sees which
/// API calls the app made and how they ended (status, network error) and reads a response body by request id, as in the
/// browser's Network tab. Events arrive on the UI thread, the agent reads on its own.
/// </summary>
public sealed class BrowserNetworkLog
{
    public const string RequestWillBeSent = "Network.requestWillBeSent";
    public const string ResponseReceived = "Network.responseReceived";
    public const string LoadingFailed = "Network.loadingFailed";
    public const int Capacity = 200;

    private const int MaxUrl = 300;

    private readonly Lock _gate = new();
    private readonly List<BrowserRequest> _requests = [];

    public static IReadOnlyList<string> Methods { get; } = [RequestWillBeSent, ResponseReceived, LoadingFailed];

    public void Record(string method, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (Text(root, "requestId") is not { } id)
        {
            return;
        }

        lock (_gate)
        {
            switch (method)
            {
                case RequestWillBeSent when root.TryGetProperty("request", out var request) && Text(request, "url") is { } url && IsHttp(url):
                    Add(new BrowserRequest(id, Text(request, "method") ?? "GET", Short(url), Text(root, "type") ?? "Other"));
                    break;
                case ResponseReceived when root.TryGetProperty("response", out var response):
                    Update(id, request => request with
                    {
                        Status = response.TryGetProperty("status", out var status) && status.TryGetInt32(out var code) ? code : null,
                        MimeType = Text(response, "mimeType"),
                    });
                    break;
                case LoadingFailed:
                    Update(id, request => request with { Error = Text(root, "errorText") ?? "failed" });
                    break;
            }
        }
    }

    /// <summary>The latest requests, oldest first.</summary>
    public IReadOnlyList<BrowserRequest> Recent(int count)
    {
        lock (_gate)
        {
            return [.. _requests.TakeLast(count)];
        }
    }

    public BrowserRequest? Find(string id)
    {
        lock (_gate)
        {
            return _requests.FindLast(request => request.Id == id);
        }
    }

    /// <summary>One line per request: <c>[id] GET 404 XHR http://localhost:5000/api/items</c>.</summary>
    public static string Describe(BrowserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outcome = request.Error ?? request.Status?.ToString(CultureInfo.InvariantCulture) ?? "…";
        return $"[{request.Id}] {request.Method} {outcome} {request.Type} {request.Url}";
    }

    private void Add(BrowserRequest request)
    {
        _requests.Add(request);
        if (_requests.Count > Capacity)
        {
            _requests.RemoveRange(0, _requests.Count - Capacity);
        }
    }

    // Redirects reuse the id: the latest entry with it is the one in flight. O(n) over at most Capacity entries.
    private void Update(string id, Func<BrowserRequest, BrowserRequest> change)
    {
        var index = _requests.FindLastIndex(request => request.Id == id);
        if (index >= 0)
        {
            _requests[index] = change(_requests[index]);
        }
    }

    private static bool IsHttp(string url) => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string Short(string url) => url.Length <= MaxUrl ? url : string.Concat(url.AsSpan(0, MaxUrl), "…");

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
