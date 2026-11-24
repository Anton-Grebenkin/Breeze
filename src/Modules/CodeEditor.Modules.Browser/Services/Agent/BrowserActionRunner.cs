using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services.Reading;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>
/// Runs one <c>browser</c> tool call (<see cref="BrowserAgentTools"/>): finds the tab, does the action and returns what
/// the model reads — a fresh snapshot after an action that changes the page, the tab the action opened, or the dialog
/// the page now waits on.
/// </summary>
public sealed class BrowserActionRunner(BrowserAgentTabs tabs, IAgentImages images, IWorkspace? workspace = null, IFileSystem? files = null, BrowserKnownSites? sites = null)
{
    /// <exception cref="AgentToolException">The call is wrong or the page refused it; the message is for the model.</exception>
    public async Task<string> RunAsync(BrowserCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        try
        {
            return await TabActionAsync(call) ?? await PageActionAsync(call, cancellationToken);
        }
        catch (BrowserException exception) when (exception.Dialog is { } dialog)
        {
            return Format(Strings.DialogOpen, dialog.Kind, dialog.Message);
        }
        catch (BrowserException exception)
        {
            throw new AgentToolException(exception.Message);
        }
    }

    // Actions on the tab list itself; null for an action on a page.
    private async Task<string?> TabActionAsync(BrowserCall call)
    {
        switch (call.Action)
        {
            case BrowserAgentTools.Tabs:
                return tabs.Describe();
            case BrowserAgentTools.SelectTab:
                var shown = tabs.Target(BrowserAgentTools.Required(call.Tab, "tab"));
                await tabs.SelectAsync(shown);
                return Format(Strings.TabSelected, shown.Id);
            case BrowserAgentTools.CloseTab:
                var closing = tabs.Target(call.Tab);
                tabs.Close(closing);
                return Format(Strings.TabClosedResult, closing.Id);
            case BrowserAgentTools.OpenTab when string.IsNullOrWhiteSpace(call.Url):
                return Format(Strings.TabOpened, tabs.Open().Id);
            default:
                return null;
        }
    }

    private async Task<string> PageActionAsync(BrowserCall call, CancellationToken cancellationToken)
    {
        var target = call.Action == BrowserAgentTools.OpenTab ? tabs.Open() : tabs.Target(call.Tab, opens: call.Action == BrowserAgentTools.Navigate);
        var browser = target.Page;
        var page = new BrowserPageActions(browser);
        switch (call.Action)
        {
            case BrowserAgentTools.Navigate or BrowserAgentTools.OpenTab:
                // The call got here, so the site is known or the user has just allowed it: it is known from now on.
                var address = BrowserAgentTools.Address(call.Url);
                sites?.Remember(address);
                await browser.NavigateAsync(address, cancellationToken);
                break;
            case BrowserAgentTools.Snapshot:
                return await SnapshotAsync(target, call.Part, cancellationToken);
            case BrowserAgentTools.Read:
                return await BrowserPageReader.TextAsync(target, call.Part, cancellationToken);
            case BrowserAgentTools.Find:
                return await BrowserPageReader.FindAsync(target, Required(call.Text, "text"), cancellationToken);
            case BrowserAgentTools.Click:
                await page.ClickAsync(Required(call.Ref, "ref"), cancellationToken);
                await browser.WaitForLoadAsync(BrowserPageActions.SettleTimeout, cancellationToken);
                break;
            case BrowserAgentTools.Hover:
                await page.HoverAsync(Required(call.Ref, "ref"), cancellationToken);
                break;
            case BrowserAgentTools.Drag:
                await page.DragAsync(Required(call.Ref, "ref"), Required(call.To, "to"), cancellationToken);
                break;
            case BrowserAgentTools.Upload:
                await page.UploadAsync(Required(call.Ref, "ref"), FilePath(call.Path), cancellationToken);
                return Format(Strings.UploadDone, call.Path!.Trim()) + "\n" + await SnapshotAsync(target, part: null, cancellationToken);
            case BrowserAgentTools.Type:
                await page.TypeAsync(Required(call.Ref, "ref"), call.Text ?? string.Empty, call.Submit, cancellationToken);
                break;
            case BrowserAgentTools.Press:
                await page.PressAsync(Required(call.Key, "key"), call.Ref, cancellationToken);
                break;
            case BrowserAgentTools.Scroll:
                await page.ScrollAsync(call.Ref, call.Direction, cancellationToken);
                break;
            case BrowserAgentTools.Dialog:
                await browser.AnswerDialogAsync(call.Accept != false, call.Text, cancellationToken);
                await browser.WaitForLoadAsync(BrowserPageActions.SettleTimeout, cancellationToken);
                break;
            case BrowserAgentTools.WaitFor:
                return await WaitForAsync(target, Required(call.Text, "text"), call.TimeoutSeconds, cancellationToken);
            case BrowserAgentTools.Back:
                await browser.GoBackAsync(cancellationToken);
                break;
            case BrowserAgentTools.Forward:
                await browser.GoForwardAsync(cancellationToken);
                break;
            case BrowserAgentTools.Reload:
                await browser.ReloadAsync(cancellationToken);
                break;
            case BrowserAgentTools.Console:
                return ConsoleText(browser.TakeConsole());
            case BrowserAgentTools.Network:
                return page.Requests();
            case BrowserAgentTools.Response:
                return await page.ResponseAsync(Required(call.Id, "id"), cancellationToken);
            case BrowserAgentTools.Evaluate:
                return await page.EvaluateAsync(Required(call.Script, "script"), cancellationToken);
            case BrowserAgentTools.Request:
                return await page.RequestAsync(target.Id, call.Method, Required(call.Url, "url"), call.Body, cancellationToken);
            case BrowserAgentTools.Screenshot:
                return await ScreenshotAsync(target, call.Ref, cancellationToken);
            default:
                throw new AgentToolException(Format(Strings.UnknownAction, call.Action, string.Join(", ", BrowserAgentTools.Actions)));
        }

        // A click that opened a window: the agent continues in that tab and is told so.
        return tabs.TakeOpened() is { } opened
            ? Format(Strings.OpenedTabNote, opened.Id) + "\n" + await SnapshotAsync(opened, part: null, cancellationToken)
            : await SnapshotAsync(target, part: null, cancellationToken);
    }

    // The snapshot comes either way: what the page shows instead of the text is often the answer.
    private async Task<string> WaitForAsync(BrowserTab tab, string text, int? seconds, CancellationToken cancellationToken)
    {
        var shown = await new BrowserPageActions(tab.Page).WaitForTextAsync(text, seconds, cancellationToken);
        var snapshot = await SnapshotAsync(tab, part: null, cancellationToken);
        var note = shown ? string.Empty : Format(Strings.TextNotShown, text, Math.Clamp(seconds ?? BrowserPageActions.DefaultWaitSeconds, 1, BrowserPageActions.MaxWaitSeconds)) + "\n";
        return note + snapshot;
    }

    private async Task<string> SnapshotAsync(BrowserTab tab, int? part, CancellationToken cancellationToken)
    {
        var snapshot = await BrowserPageReader.SnapshotAsync(tab, part, cancellationToken);
        tabs.RememberLinks(tab, snapshot);
        return snapshot;
    }

    // Only a shown page renders, so the tab comes to the front first. The image goes to the model in the next message;
    // a PNG over the model's limit is retried as JPEG.
    private async Task<string> ScreenshotAsync(BrowserTab tab, string? reference, CancellationToken cancellationToken)
    {
        if (!images.CanShow)
        {
            return Strings.ScreenshotNotSeen;
        }

        await tabs.SelectAsync(tab);
        var page = tab.Page.Url?.AbsoluteUri ?? BrowserAgentTools.ToolName;
        var shown = images.TryShow(page, await CaptureAsync(BrowserImageFormat.Png), "image/png")
            || images.TryShow(page, await CaptureAsync(BrowserImageFormat.Jpeg), "image/jpeg");
        return shown ? Format(Strings.ScreenshotResult, page) : throw new AgentToolException(Strings.ScreenshotTooLarge);

        Task<byte[]> CaptureAsync(BrowserImageFormat format) => string.IsNullOrWhiteSpace(reference)
            ? tab.Page.CaptureAsync(format, cancellationToken)
            : BrowserPageReader.CaptureElementAsync(tab, reference.Trim(), format, cancellationToken);
    }

    // A file of the folder, never a secret: what goes into the field leaves the machine with the form.
    private string FilePath(string? path)
    {
        var full = WorkspacePaths.Resolve(workspace ?? throw new AgentToolException(Strings.UploadNeedsFolder), Required(path, "path"));
        SensitivePaths.EnsureReadable(workspace.RelativePath(full));
        return files?.FileExists(full) != false ? full : throw new AgentToolException(Format(Strings.UploadNoFile, path!));
    }

    private static string ConsoleText(IReadOnlyList<BrowserConsoleMessage> messages) =>
        messages.Count == 0
            ? Strings.ConsoleEmpty
            : string.Join('\n', messages.Select(message => $"[{message.Level}] {message.Text}"));

    private static string Required(string? value, string argument) => BrowserAgentTools.Required(value, argument);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
