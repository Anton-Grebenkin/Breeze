using System.Net;
using System.Net.Sockets;
using System.Text;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Panels;

/// <summary>
/// Browser on real WebView2 (ADR 0027): opens from the View menu as an editor tab (ADR 0031), an address without a
/// scheme is completed, and a local page loads and is visible: its text is in the window's accessibility tree.
/// </summary>
public sealed class BrowserTests(AppSession session) : IClassFixture<AppSession>
{
    private const string ShowBrowserCommand = "workbench.view.browser";

    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void ViewMenu_OpensBrowser_LocalPageLoads()
    {
        using var page = LocalPage.Start("<html><head><title>Проверка браузера</title></head><body><h1>Привет из браузера</h1><button>Нажми</button></body></html>");

        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem(ShowBrowserCommand)).GuardedClick();
        session.TypeInto("Browser.Address", $"localhost:{page.Port}/");
        Keyboard.Type(VirtualKeyShort.ENTER);

        Assert.True(Retry.WhileNull(() => session.MainWindow.FindFirstDescendant(condition => condition.ByName("Привет из браузера")), PageTimeout).Success,
            "Текст страницы не появился в окне.");
        session.WaitForValue("Browser.Address", value => value.StartsWith($"http://localhost:{page.Port}/", StringComparison.Ordinal), PageTimeout);
        session.SaveScreenshot("browser-panel");

        session.Find("EditorTab.Браузер.Close").GuardedClick();
        session.WaitUntilGone("Browser.Address");
    }

    // The page is a native window over WPF; the palette over a browser tab must not go under it (ADR 0031).
    [Fact]
    public void Palette_OverTheBrowserTab_IsNotCoveredByThePage()
    {
        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem(ShowBrowserCommand)).GuardedClick();
        session.WaitFor("Browser.Address");
        PaletteAirspace.AssertPaletteIsNotCovered(session);

        session.Find("EditorTab.Браузер.Close").GuardedClick();
        session.WaitUntilGone("Browser.Address");
    }

    /// <summary>A page on a free local port that answers every request with the same HTML.</summary>
    private sealed class LocalPage : IDisposable
    {
        private readonly HttpListener _listener = new();

        private LocalPage(string html)
        {
            Port = FreePort();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
            _ = ServeAsync(Encoding.UTF8.GetBytes(html));
        }

        public int Port { get; }

        public static LocalPage Start(string html) => new(html);

        public void Dispose() => _listener.Close();

        private async Task ServeAsync(byte[] body)
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        }

        private static int FreePort()
        {
            using var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }
    }
}
