using CodeEditor.Modules.Browser.Services;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// The browser's proxy comes from the user's settings as a process switch: only an address of the expected shape
/// passes, so the setting cannot add a switch of its own.
/// </summary>
public sealed class BrowserEngineArgumentsTests
{
    [Fact]
    public void NoProxy_NoSwitch() => Assert.Equal(string.Empty, BrowserEngineArguments.Build(new BrowserOptions()));

    [Fact]
    public void Proxy_BecomesASwitch() =>
        Assert.Equal("--proxy-server=http://proxy.corp.example:3128", BrowserEngineArguments.Build(new BrowserOptions { Proxy = " http://proxy.corp.example:3128 " }));

    [Theory]
    [InlineData("socks5://127.0.0.1:1080", "socks5://127.0.0.1:1080")]
    [InlineData("https://proxy.example", "https://proxy.example:443")]
    [InlineData("proxy.example:3128", null)]
    [InlineData("http://proxy.example:3128 --remote-debugging-port=9222", null)]
    [InlineData("file:///C:/x", null)]
    [InlineData("", null)]
    public void Proxy_IsAnAddress_OrNothing(string setting, string? expected) => Assert.Equal(expected, BrowserEngineArguments.Proxy(setting));
}
