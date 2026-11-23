using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Reading;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>A fake page: records navigations and scripts, returns a preset snapshot and action result.</summary>
internal sealed class FakeBrowserPage : IBrowserPage
{
    public FakeBrowserPage(BrowserPageState? restored = null)
    {
        if (restored is not null)
        {
            (Url, Title) = (restored.Url, restored.Title);
        }
    }

    public bool IsDisposed { get; private set; }

    /// <summary>The page was made for the agent: it holds dialogs for the agent's answer.</summary>
    public bool IsForAgent { get; init; }

    public BrowserDialog? Dialog { get; set; }

    /// <summary>Answers given to dialogs: accepted or not, and the text.</summary>
    public List<(bool Accept, string? Text)> DialogAnswers { get; } = [];

    public Task AnswerDialogAsync(bool accept, string? text, CancellationToken cancellationToken)
    {
        if (Dialog is null)
        {
            return Task.FromException(new BrowserException("нет диалога"));
        }

        DialogAnswers.Add((accept, text));
        Dialog = null;
        return Task.CompletedTask;
    }

    public ReadOnlyMemory<byte> Favicon { get; set; }

    public BrowserNetworkLog Network { get; } = new();

    public event EventHandler<BrowserPopupEventArgs>? PopupRequested;

    public event EventHandler? CloseRequested;

    public void Dispose() => IsDisposed = true;

    /// <summary>The page opens a window; the page given to it by the tabs comes back.</summary>
    public FakeBrowserPage? OpenWindow(string? url = null)
    {
        var request = new BrowserPopupEventArgs(url is null ? null : new Uri(url));
        PopupRequested?.Invoke(this, request);
        return (FakeBrowserPage?)request.Page;
    }

    /// <summary>The page closes itself (<c>window.close()</c>).</summary>
    public void CloseWindow() => CloseRequested?.Invoke(this, EventArgs.Empty);

    public List<Uri> Navigations { get; } = [];

    public List<string> Scripts { get; } = [];

    public List<BrowserConsoleMessage> Console { get; } = [];

    /// <summary>Result of click and type.</summary>
    public string ActionResult { get; set; } = """{"ok":true}""";

    public string SnapshotResult { get; set; } = """{"title":"Вход","url":"http://localhost:5000/login","text":"- heading \"Вход\"\n- textbox \"Email\" [ref=e1]","truncated":false}""";

    /// <summary>Result of <c>read</c>: the page's main text as Markdown.</summary>
    public string MarkdownResult { get; set; } = """{"title":"Вход","url":"http://localhost:5000/login","text":"# Вход\n\nВведите почту.","truncated":false}""";

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
        StatusCode ??= 200;
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

    /// <summary>Runs when the page gets a script: a test makes the page react (open a window on a click).</summary>
    public Action<string>? OnScript { get; set; }

    /// <summary>What the page answers about the point to click: <c>element</c>, <c>file</c> or <c>select</c>.</summary>
    public string PointKind { get; set; } = "element";

    public bool IsDraggable { get; set; }

    /// <summary>The real mouse click reached the element; otherwise the tool falls back to a script click.</summary>
    public bool ClickReached { get; set; } = true;

    public Task<string> EvaluateAsync(string script, CancellationToken cancellationToken)
    {
        // A page that shows a dialog answers nothing until the dialog is answered.
        if (Dialog is { } dialog)
        {
            return Task.FromException<string>(new BrowserException(dialog.Message) { Dialog = dialog });
        }

        Scripts.Add(script);
        OnScript?.Invoke(script);
        if (script.Contains("window.__agentClick = null", StringComparison.Ordinal))
        {
            return Task.FromResult(ActionResult.Contains("false", StringComparison.Ordinal)
                ? ActionResult
                : $$"""{"ok":true,"x":120,"y":40,"kind":"{{PointKind}}","draggable":{{(IsDraggable ? "true" : "false")}}}""");
        }

        if (script.StartsWith("window.__agentClick ===", StringComparison.Ordinal))
        {
            return Task.FromResult(ClickReached ? "true" : "false");
        }

        if (script == BrowserSnapshot.Script)
        {
            return Task.FromResult(SnapshotResult);
        }

        if (script == BrowserReading.Markdown)
        {
            return Task.FromResult(MarkdownResult);
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
        StatusCode ??= 200;
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
