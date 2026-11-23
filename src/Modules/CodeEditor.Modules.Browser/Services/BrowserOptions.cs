namespace CodeEditor.Modules.Browser.Services;

/// <summary>Browser module settings: the <c>browser</c> section of settings.json.</summary>
public sealed class BrowserOptions
{
    public const string Section = "browser";

    /// <summary>Sites beyond this machine that the agent opens without asking; "Always allow" adds a site here.</summary>
    public List<string> AllowedHosts { get; set; } = [];

    /// <summary>The agent is told which sites are open in the browser; the chip above the chat input switches it.</summary>
    public bool ShareTabsWithAgent { get; set; } = true;
}
