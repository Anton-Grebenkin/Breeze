using System.Runtime.InteropServices;
using CodeEditor.Modules.Browser.Services;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>
/// What a person does with a page besides reading it: find text on it, zoom, open the developer tools or the
/// downloads, and pick an element with the mouse to show the agent what they mean.
/// </summary>
public sealed partial class WebViewPage
{
    // The engine reports the match numbers a moment after a step.
    private static readonly TimeSpan FindSettle = TimeSpan.FromMilliseconds(80);

    private string? _findText;
    private TaskCompletionSource<BrowserPickedElement?>? _pick;
    private string? _pickToken;

    public double Zoom { get; private set; } = 1;

    public Task SetZoomAsync(double zoom, CancellationToken cancellationToken) => _host.Dispatcher.InvokeAsync(() =>
    {
        Zoom = zoom;
        if (_control is not null)
        {
            _control.ZoomFactor = zoom;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    });

    public async Task<BrowserFindResult> FindAsync(string text, BrowserFindStep step, CancellationToken cancellationToken)
    {
        var core = await PageAsync(show: false, cancellationToken);
        Task? started = null;
        await _host.Dispatcher.InvokeAsync(() =>
        {
            var find = core.Find;
            if (string.IsNullOrEmpty(text))
            {
                find.Stop();
                _findText = null;
            }
            else if (step == BrowserFindStep.Start || text != _findText)
            {
                // Our own bar asks, so the browser's one stays away.
                var options = core.Environment.CreateFindOptions();
                (options.FindTerm, options.SuppressDefaultFindDialog, options.ShouldHighlightAllMatches) = (text, true, true);
                _findText = text;
                started = find.StartAsync(options);
            }
            else if (step == BrowserFindStep.Next)
            {
                find.FindNext();
            }
            else
            {
                find.FindPrevious();
            }
        });

        // A search runs until it is stopped, so its start is not awaited to the end: the numbers settle shortly.
        await Task.WhenAny(started ?? Task.CompletedTask, Task.Delay(FindSettle, cancellationToken));
        await Task.Delay(FindSettle, cancellationToken);
        BrowserFindResult result = default;
        // The engine numbers the matches from 1.
        await _host.Dispatcher.InvokeAsync(() => result = _findText is null ? default : new BrowserFindResult(core.Find.MatchCount == 0 ? 0 : core.Find.ActiveMatchIndex, core.Find.MatchCount));
        return result;
    }

    public async Task OpenAsync(BrowserWindowKind kind, CancellationToken cancellationToken)
    {
        var core = await ReadyAsync(show: false, cancellationToken);
        await _host.Dispatcher.InvokeAsync(() =>
        {
            if (kind == BrowserWindowKind.DevTools)
            {
                core.OpenDevToolsWindow();
            }
            else
            {
                core.OpenDefaultDownloadDialog();
            }
        });
    }

    public async Task<BrowserPickedElement?> PickElementAsync(CancellationToken cancellationToken)
    {
        var core = await PageAsync(show: false, cancellationToken);
        var pick = new TaskCompletionSource<BrowserPickedElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _host.Dispatcher.InvokeAsync(() =>
        {
            EndPick(picked: null);
            (_pick, _pickToken) = (pick, Guid.NewGuid().ToString("N"));
            _ = core.ExecuteScriptAsync(BrowserPicker.Start(_pickToken));
        });

        await using var cancelled = cancellationToken.Register(() => _host.Dispatcher.Post(() =>
        {
            if (ReferenceEquals(_pick, pick))
            {
                EndPick(picked: null);
                _ = Quietly(() => core.ExecuteScriptAsync(BrowserPicker.Stop));
            }
        }));
        return await pick.Task;
    }

    // Only the answer to the pick that is waiting counts: a page cannot send a pick of its own.
    private void OnWebMessage(string json)
    {
        if (_pickToken is not { } token)
        {
            return;
        }

        var picked = BrowserPicker.Parse(json, token, out var cancelled);
        if (picked is not null || cancelled)
        {
            EndPick(picked);
        }
    }

    private void EndPick(BrowserPickedElement? picked)
    {
        var pick = _pick;
        (_pick, _pickToken) = (null, null);
        pick?.TrySetResult(picked);
    }

    private static async Task Quietly(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ObjectDisposedException)
        {
            // The page is gone: there is nothing left to stop.
        }
    }
}
