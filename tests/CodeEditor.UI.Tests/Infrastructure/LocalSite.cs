using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>A site on a free local port for browser tests: answers each path with its HTML, 404 for the rest.</summary>
public sealed class LocalSite : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly IReadOnlyDictionary<string, string> _pages;

    private LocalSite(IReadOnlyDictionary<string, string> pages)
    {
        _pages = pages;
        Port = FreePort();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Start();
        _ = ServeAsync();
    }

    public int Port { get; }

    /// <param name="pages">HTML by path, like <c>/</c> or <c>/second</c>.</param>
    public static LocalSite Start(IReadOnlyDictionary<string, string> pages) => new(pages);

    public string Address(string path) => $"localhost:{Port}{path}";

    public void Dispose() => _listener.Close();

    private async Task ServeAsync()
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

            var found = _pages.TryGetValue(context.Request.Url?.AbsolutePath ?? "/", out var html);
            context.Response.StatusCode = found ? 200 : 404;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(html ?? "<html><body>404</body></html>"));
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
