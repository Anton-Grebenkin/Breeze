namespace CodeEditor.Modules.Agent.Services.Web;

/// <summary>A fetched page: the URL after redirects, the content type and the text for the model.</summary>
public sealed record WebPage(Uri Url, string ContentType, string Text);
