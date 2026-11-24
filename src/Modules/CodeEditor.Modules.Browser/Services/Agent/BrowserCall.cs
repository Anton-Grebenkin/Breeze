namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>The arguments of one <c>browser</c> tool call, as the model gave them.</summary>
public sealed record BrowserCall(string Action)
{
    public string? Url { get; init; }

    public string? Tab { get; init; }

    public string? Ref { get; init; }

    /// <summary>The element a drag ends on.</summary>
    public string? To { get; init; }

    public string? Text { get; init; }

    public bool Submit { get; init; }

    public string? Key { get; init; }

    public string? Direction { get; init; }

    public string? Script { get; init; }

    /// <summary>The HTTP method of a request; GET when empty.</summary>
    public string? Method { get; init; }

    /// <summary>The body of a request, usually JSON.</summary>
    public string? Body { get; init; }

    public string? Id { get; init; }

    /// <summary>The file for a file field, relative to the folder.</summary>
    public string? Path { get; init; }

    /// <summary>The answer to a page dialog; accepted unless <c>false</c>.</summary>
    public bool? Accept { get; init; }

    public int? TimeoutSeconds { get; init; }

    public int? Part { get; init; }
}
