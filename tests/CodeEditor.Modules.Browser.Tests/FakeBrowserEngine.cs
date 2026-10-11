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

    public int? StatusCode { get; set; }

    /// <summary>DevTools calls: method and parameters, in order.</summary>
    public List<(string Method, string Parameters)> DevTools { get; } = [];

    /// <summary>DevTools replies by method; <c>{}</c> otherwise.</summary>
    public Dictionary<string, string> DevToolsReplies { get; } = [];

    /// <summary>Replies to scripts other than the snapshot, by script; <see cref="ActionResult"/> otherwise.</summary>
    public Dictionary<string, Queue<string>> ScriptReplies { get; } = [];

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
        if (script == BrowserScripts.Snapshot)
        {
            return Task.FromResult(SnapshotResult);
        }

        return Task.FromResult(ScriptReplies.TryGetValue(script, out var replies) && replies.Count > 0 ? replies.Dequeue() : ActionResult);
    }

    public Task<string> DevToolsAsync(string method, string parameters, CancellationToken cancellationToken)
    {
        DevTools.Add((method, parameters));
        return Task.FromResult(DevToolsReplies.GetValueOrDefault(method, "{}"));
    }

    /// <summary>A page load as the engine reports it: the address, title, then the end of loading.</summary>
    public void Load(string url, string title)
    {
        Url = new Uri(url);
        Title = title;
        IsLoading = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
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
