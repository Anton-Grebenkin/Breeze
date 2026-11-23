namespace CodeEditor.Modules.Agent.Contracts.Web;

/// <summary>
/// The web through the editor's browser, for the agent's <c>web_search</c> and <c>web_fetch</c> when the model service
/// has no search of its own or a site refuses a plain request: a real browser with the user's network, proxy and
/// cookies gets what a bare HTTP client does not. Implemented by the browser module.
/// </summary>
public interface IWebBrowserAccess
{
    /// <summary>Opens a search engine's results for the query and reads them.</summary>
    /// <exception cref="AgentToolException">The browser or the engine is unavailable, or the page needs the user (a captcha).</exception>
    Task<IReadOnlyList<WebSearchHit>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>Opens the page and returns its main text as Markdown.</summary>
    /// <exception cref="AgentToolException">The page did not open.</exception>
    Task<string> ReadAsync(Uri url, CancellationToken cancellationToken);
}
