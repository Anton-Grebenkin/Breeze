using System.Globalization;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// Audio and video in a WebView2 page (ADR 0037): the Chromium player with controls, codecs as in Edge. No autoplay:
/// the recording stays paused until started. The page reports duration and frame size once it reads the header; an
/// unsupported codec shows a clear message and "Open in external app".
/// </summary>
public sealed partial class MediaViewerViewModel(string filePath, ViewerKind kind, ViewerContext context)
    : WebViewerViewModel(filePath, kind, context)
{
    private const string Separator = " · ";
    private const int SecondsPerHour = 3600;

    public bool IsVideo => Kind == ViewerKind.Video;

    /// <summary>Recording duration; <c>null</c> if unknown (a stream) or not read yet.</summary>
    [ObservableProperty]
    public partial TimeSpan? Duration { get; private set; }

    /// <summary>Video frame size.</summary>
    [ObservableProperty]
    public partial PixelSize? FrameSize { get; private set; }

    /// <summary>The built-in player cannot play the recording: unsupported codec or format.</summary>
    [ObservableProperty]
    public partial bool CannotPlay { get; private set; }

    public override void Receive(ViewerPageMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        switch (message.Type)
        {
            case ViewerPageMessages.Metadata:
                Duration = message.Duration is { } seconds && double.IsFinite(seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
                FrameSize = message is { Width: { } width and > 0, Height: { } height and > 0 } ? new PixelSize((int)width, (int)height) : null;
                CannotPlay = false;
                ShowError(null);
                UpdateSummary();
                break;
            case ViewerPageMessages.Error:
                CannotPlay = true;
                ShowError(Strings.MediaUnsupported);
                break;
            default:
                break;
        }
    }

    protected override void UpdateSummary()
    {
        var format = Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();
        var parts = new List<string> { Format(IsVideo ? Strings.VideoFormat : Strings.AudioFormat, format) };
        if (FrameSize is { } size)
        {
            parts.Add(Format(Strings.PixelDimensions, size.Width, size.Height));
        }

        if (Duration is { } duration)
        {
            parts.Add(FormatDuration(duration));
        }

        parts.Add(ByteSizes.Format(FileLength));
        Summary = string.Join(Separator, parts);
    }

    /// <summary>"3:07" or "1:02:07", as in media players; hours never roll over into days.</summary>
    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalSeconds >= SecondsPerHour
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{duration.Minutes}:{duration.Seconds:00}");
}
