using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.ViewModels;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// Audio and video in a WebView2 page (<see cref="WebViewerHost"/>). Visual logic: page replies go to the view model, and
/// hiding the tab pauses the recording so no sound plays from an invisible tab.
/// </summary>
public sealed partial class MediaViewerView
{
    private readonly WebViewerHost _page;

    public MediaViewerView(WebViewerServices services)
    {
        InitializeComponent();
        _page = new WebViewerHost(this, Host, Unavailable, services, new WebPageKind(ViewerAddresses.MediaPage, "MediaViewer.Page", Strings.Player, ShowMessage));
        _page.MessageReceived += (_, message) => _page.ViewModel?.Receive(message);
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                _page.Post(ViewerPageMessages.Pause);
            }
        };
    }

    private static string ShowMessage(WebViewerViewModel viewModel, Uri source, IReadOnlyDictionary<string, string> theme) =>
        ViewerPageMessages.ShowMedia(source, ((MediaViewerViewModel)viewModel).IsVideo, theme);
}
