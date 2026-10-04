using CodeEditor.Modules.Browser.Services;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>A fake browser: records navigations and scripts, returns a preset snapshot and action result.</summary>
internal sealed class FakeBrowserEngine : IBrowserEngine
{
    public List<Uri> Navigations { get; } = [];

    public List<string> Scripts { get; } = [];

    public List<BrowserConsoleMessage> Console { get; } = [];

    /// <summary>Result of click and type.</summary>
    public string ActionResult { get; set; } = """{"ok":true}""";

    public string SnapshotResult { get; set; } = """{"title":"Вход","url":"http://localhost:5000/login","text":"- heading \"Вход\"\n- textbox \"Email\" [ref=e1]","truncated":false}""";

    public Exception? Failure { get; set; }

    /// <summary>Screenshot per format; requested formats are recorded in <see cref="Captures"/>.</summary>
    public Dictionary<BrowserImageFormat, byte[]> Screenshots { get; } = new() { [BrowserImageFormat.Png] = [1, 2, 3], [BrowserImageFormat.Jpeg] = [4, 5] };

    public List<BrowserImageFormat> Captures { get; } = [];

    public int Waits { get; private set; }

    public Uri? Url { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public bool CanGoBack { get; set; }

    public bool CanGoForward { get; set; }

    public bool IsLoading { get; set; }

    public event EventHandler? StateChanged;

    public Task NavigateAsync(Uri url, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        Navigations.Add(url);
        Url = url;
        Title = "Страница";
        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task GoBackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task GoForwardAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ReloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task WaitForLoadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Waits++;
        return Task.CompletedTask;
    }

    public Task<string> EvaluateAsync(string script, CancellationToken cancellationToken)
    {
        Scripts.Add(script);
        return Task.FromResult(script == BrowserScripts.Snapshot ? SnapshotResult : ActionResult);
    }

    public Task<byte[]> CaptureAsync(BrowserImageFormat format, CancellationToken cancellationToken)
    {
        Captures.Add(format);
        return Task.FromResult(Screenshots[format]);
    }

    public IReadOnlyList<BrowserConsoleMessage> TakeConsole()
    {
        var taken = Console.ToList();
        Console.Clear();
        return taken;
    }
}
