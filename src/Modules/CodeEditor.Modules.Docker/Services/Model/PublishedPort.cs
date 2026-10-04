using System.Globalization;

namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>
/// A container port published on the host: "0.0.0.0:8080->80/tcp". The browser address is <c>localhost</c> when the
/// port listens on all host addresses, otherwise the address itself; port 443 uses https.
/// </summary>
/// <param name="Host">Host address from docker: "0.0.0.0", "::", "127.0.0.1".</param>
public readonly record struct PublishedPort(string Host, int HostPort, int ContainerPort, string Protocol)
{
    private const int HttpsPort = 443;

    /// <summary>tcp ports open in the browser; udp ones do not.</summary>
    public bool IsTcp => Protocol == "tcp";

    /// <summary>":8080", the link in a container row.</summary>
    public string Label => ":" + HostPort.ToString(CultureInfo.InvariantCulture);

    /// <summary>"8080 → 80/tcp", for the tooltip and port list.</summary>
    public string Mapping => string.Create(CultureInfo.InvariantCulture, $"{HostPort} → {ContainerPort}/{Protocol}");

    public Uri Url => new UriBuilder(HostPort == HttpsPort ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, BrowserHost, HostPort).Uri;

    private string BrowserHost => Host is "" or "0.0.0.0" or "::" or "[::]" ? "localhost" : Host;
}
