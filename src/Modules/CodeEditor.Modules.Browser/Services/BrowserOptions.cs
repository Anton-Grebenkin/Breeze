namespace CodeEditor.Modules.Browser.Services;

/// <summary>Browser module settings: the <c>browser</c> section of settings.json.</summary>
public sealed class BrowserOptions
{
    public const string Section = "browser";

    /// <summary>
    /// Sites the agent opens without asking, besides this machine and the sites the browser remembers by itself (the
    /// ones the user opened or let the agent open once): a list to fill in by hand.
    /// </summary>
    public List<string> AllowedHosts { get; set; } = [];

    /// <summary>
    /// Sites where the agent presses buttons and fills in fields in its own tab without asking; "Always allow" on such
    /// a card adds a site here. Elsewhere the agent only reads and follows links.
    /// </summary>
    public List<string> ActHosts { get; set; } = [];

    /// <summary>
    /// The search engine for the agent's search through the browser: <c>duckduckgo</c>, <c>bing</c>, <c>google</c>,
    /// <c>yandex</c>, or an address of one's own with <c>{query}</c> where the query goes.
    /// </summary>
    public string SearchEngine { get; set; } = "duckduckgo";

    /// <summary>
    /// The browser offers to save passwords and fills them in, as an ordinary browser does; saved sign-ins stay in the
    /// editor's browser profile.
    /// </summary>
    public bool SavePasswords { get; set; } = true;

    /// <summary>
    /// The proxy for the browser, like <c>http://proxy.corp.example:3128</c> or <c>socks5://127.0.0.1:1080</c>; empty
    /// for the system's one. Applied when the editor starts.
    /// </summary>
    public string Proxy { get; set; } = string.Empty;

    /// <summary>The agent is told which sites are open in the browser; the chip above the chat input switches it.</summary>
    public bool ShareTabsWithAgent { get; set; } = true;
}
