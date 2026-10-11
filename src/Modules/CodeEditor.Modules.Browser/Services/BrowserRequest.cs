namespace CodeEditor.Modules.Browser.Services;

/// <summary>A request of the open page (<see cref="BrowserNetworkLog"/>), filled in as its response or failure arrives.</summary>
/// <param name="Type">DevTools resource type: <c>Document</c>, <c>XHR</c>, <c>Fetch</c>, <c>Script</c>, <c>Image</c>…</param>
public sealed record BrowserRequest(string Id, string Method, string Url, string Type)
{
    /// <summary>HTTP status; <c>null</c> until the response arrives.</summary>
    public int? Status { get; init; }

    public string? MimeType { get; init; }

    /// <summary>Why the request failed (<c>net::ERR_CONNECTION_REFUSED</c>); <c>null</c> otherwise.</summary>
    public string? Error { get; init; }
}
