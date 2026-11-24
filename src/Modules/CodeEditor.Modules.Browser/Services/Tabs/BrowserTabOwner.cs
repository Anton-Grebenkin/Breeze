namespace CodeEditor.Modules.Browser.Services.Tabs;

/// <summary>Who opened a tab: the agent reads the user's tabs freely but changes them only with approval.</summary>
public enum BrowserTabOwner
{
    User,
    Agent,
}
