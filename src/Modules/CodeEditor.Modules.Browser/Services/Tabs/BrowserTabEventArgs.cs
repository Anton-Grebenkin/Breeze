namespace CodeEditor.Modules.Browser.Services.Tabs;

public sealed class BrowserTabEventArgs(BrowserTab tab) : EventArgs
{
    public BrowserTab Tab { get; } = tab;
}
