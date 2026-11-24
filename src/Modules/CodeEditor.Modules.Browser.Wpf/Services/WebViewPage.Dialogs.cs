using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using Microsoft.Web.WebView2.Core;

namespace CodeEditor.Modules.Browser.Wpf.Services;

/// <summary>
/// Page dialogs (<c>alert</c>, <c>confirm</c>, <c>prompt</c>) in a page of the agent's: the browser's own window is
/// off, the dialog is held until the agent answers it, and a call the dialog interrupted returns at once with the
/// dialog instead of waiting for a timeout. Leaving a page that asks "leave the site?" is the agent's own navigation
/// and is accepted. In the user's pages the browser shows its dialogs as usual.
/// </summary>
public sealed partial class WebViewPage
{
    private CoreWebView2ScriptDialogOpeningEventArgs? _dialogRequest;
    private CoreWebView2Deferral? _dialogDeferral;
    private TaskCompletionSource _dialogOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BrowserDialog? Dialog { get; private set; }

    public Task AnswerDialogAsync(bool accept, string? text, CancellationToken cancellationToken) => _host.Dispatcher.InvokeAsync(() =>
    {
        if (_dialogRequest is not { } request)
        {
            throw new BrowserException(Strings.NoDialog);
        }

        if (accept)
        {
            request.ResultText = text ?? request.DefaultText;
            request.Accept();
        }

        CloseDialog();
    });

    private void HoldDialogs(CoreWebView2 core)
    {
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.ScriptDialogOpening += (_, e) =>
        {
            if (e.Kind == CoreWebView2ScriptDialogKind.Beforeunload)
            {
                e.Accept();
                return;
            }

            (_dialogRequest, _dialogDeferral) = (e, e.GetDeferral());
            Dialog = new BrowserDialog(e.Kind.ToString().ToLowerInvariant(), e.Message, e.DefaultText);
            _dialogOpened.TrySetResult();
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    private void CloseDialog()
    {
        _dialogDeferral?.Complete();
        (_dialogRequest, _dialogDeferral, Dialog) = (null, null, null);
        _dialogOpened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // The call's own task goes on once the dialog is answered; its result is no longer awaited.
    private async Task<string> UnlessDialogAsync(Task<string> pending, CancellationToken cancellationToken)
    {
        var opened = _dialogOpened.Task;
        if (await Task.WhenAny(pending, opened).WaitAsync(ScriptTimeout, cancellationToken) == opened && !pending.IsCompleted && Dialog is { } dialog)
        {
            _ = pending.ContinueWith(static task => task.Exception, TaskScheduler.Default);
            throw new BrowserException(dialog.Message) { Dialog = dialog };
        }

        return await pending;
    }
}
