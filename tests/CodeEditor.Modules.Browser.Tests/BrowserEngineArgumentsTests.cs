using CodeEditor.Modules.Browser.Services;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// The browser's proxy and Windows sign-in servers come from the user's settings as process switches: only values of
/// the expected shape pass, so a setting cannot add a switch of its own.
/// </summary>
public sealed class BrowserEngineArgumentsTests
{
    [Fact]
    public void NoSettings_NoSwitches() => Assert.Equal(string.Empty, BrowserEngineArguments.Build(new BrowserOptions()));

    [Fact]
    public void ProxyAndSignInServers_BecomeSwitches()
    {
        var options = new BrowserOptions { Proxy = " http://proxy.corp.example:3128 ", AuthServers = ["*.corp.example", "gitlab.corp.example"] };

        Assert.Equal("--proxy-server=http://proxy.corp.example:3128 --auth-server-allowlist=*.corp.example,gitlab.corp.example", BrowserEngineArguments.Build(options));
    }

    [Theory]
    [InlineData("socks5://127.0.0.1:1080", "socks5://127.0.0.1:1080")]
    [InlineData("https://proxy.example", "https://proxy.example:443")]
    [InlineData("proxy.example:3128", null)]
    [InlineData("http://proxy.example:3128 --remote-debugging-port=9222", null)]
    [InlineData("file:///C:/x", null)]
    [InlineData("", null)]
    public void Proxy_IsAnAddress_OrNothing(string setting, string? expected) => Assert.Equal(expected, BrowserEngineArguments.Proxy(setting));

    // A value with a space or a quote could start another switch: it is dropped.
    [Fact]
    public void SignInServer_ThatIsNotAHostPattern_IsDropped()
    {
        var options = new BrowserOptions { AuthServers = ["*.corp.example --disable-web-security", "\"x\"", "ok.example"] };

        Assert.Equal("--auth-server-allowlist=ok.example", BrowserEngineArguments.Build(options));
    }
}
