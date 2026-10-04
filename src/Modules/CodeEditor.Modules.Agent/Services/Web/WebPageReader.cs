using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Web;

/// <summary>
/// Fetches a page for the <c>web_fetch</c> tool: GET with size and time limits, redirects as in a browser. HTML becomes
/// text (<see cref="HtmlText"/>), text, Markdown and JSON pass as is; images, PDFs and archives are refused with their
/// content type. One <see cref="HttpClient"/> for the app's lifetime, so connections are reused.
/// </summary>
public sealed class WebPageReader : IDisposable
{
    /// <summary>Pages are read up to this size; the rest is dropped.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly string[] TextTypes = ["text/", "application/json", "application/xml", "application/xhtml+xml", "application/javascript"];

    private readonly HttpClient _http;

    static WebPageReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public WebPageReader()
        : this(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
    }

    /// <param name="handler">Transport; a fake server in tests.</param>
    internal WebPageReader(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = Timeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("CodeEditor/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html, text/markdown, text/plain, application/json;q=0.9, */*;q=0.5");
    }

    /// <exception cref="AgentToolException">The URL is not http(s), the site is unreachable or returned an error, or the content is not text.</exception>
    public async Task<WebPage> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme is not ("http" or "https"))
        {
            throw new AgentToolException(Format(Strings.WebOnlyHttp, url));
        }

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AgentToolException(Format(Strings.WebHttpError, url, (int)response.StatusCode, response.ReasonPhrase ?? string.Empty));
            }

            var type = response.Content.Headers.ContentType;
            var media = type?.MediaType ?? "text/html";
            if (!TextTypes.Any(prefix => media.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                throw new AgentToolException(Format(Strings.WebNotText, url, media));
            }

            var text = Decode(await ReadLimitedAsync(response.Content, cancellationToken), type);
            var final = response.RequestMessage?.RequestUri ?? url;
            return new WebPage(final, media, media.Contains("html", StringComparison.OrdinalIgnoreCase) ? HtmlText.ToText(text, final) : text);
        }
        catch (HttpRequestException exception)
        {
            throw new AgentToolException(Format(Strings.WebUnreachable, url, exception.Message));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AgentToolException(Format(Strings.WebTimedOut, url, (int)Timeout.TotalSeconds));
        }
    }

    public void Dispose() => _http.Dispose();

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while (buffer.Length < MaxBytes && (read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, MaxBytes - buffer.Length)), cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    // Encoding from the header, otherwise UTF-8: pages in other encodings without a header are rare.
    private static string Decode(byte[] bytes, MediaTypeHeaderValue? type)
    {
        var encoding = Encoding.UTF8;
        if (type?.CharSet is { Length: > 0 } charset)
        {
            try
            {
                encoding = Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
                // Unknown encoding in the header: read as UTF-8.
            }
        }

        return encoding.GetString(bytes);
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
