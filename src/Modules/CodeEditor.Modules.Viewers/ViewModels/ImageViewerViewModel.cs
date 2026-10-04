using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// Image viewer (ADR 0037): Windows decoder in the background, zoom to fit, 100 %, steps and wheel
/// (<see cref="ZoomState"/>); info shows pixel size, format and file size. A large image (over
/// <see cref="KeepWhenHiddenBytes"/> in memory) is released while its tab is hidden and decoded again when shown, so a
/// dozen open photos do not hold hundreds of megabytes.
/// </summary>
public sealed partial class ImageViewerViewModel(string filePath, ViewerContext context)
    : ViewerViewModel(filePath, ViewerKind.Image, context), IZoomableViewer
{
    /// <summary>A hidden tab does not keep an image larger than this in memory (2048 × 2048 pixels).</summary>
    public const long KeepWhenHiddenBytes = 16L * 1024 * 1024;

    private const string NoteSeparator = " ";

    private bool _hidden;
    private bool _released;

    public ZoomState Zoom { get; } = new();

    /// <summary>The picture to show (a frozen <c>BitmapSource</c> in WPF); <c>null</c> if not decoded yet or released.</summary>
    [ObservableProperty]
    public partial object? Picture { get; private set; }

    /// <summary>The last decoded image: size, format, frames.</summary>
    [ObservableProperty]
    public partial DecodedImage? Image { get; private set; }

    /// <summary>The image cannot be shown but an external app might show it: no codec or a damaged file.</summary>
    [ObservableProperty]
    public partial bool SuggestsExternalApp { get; private set; }

    public override void SetShown(bool shown)
    {
        _hidden = !shown;
        if (!shown)
        {
            ReleaseIfLarge();
        }
        else if (_released)
        {
            _released = false;
            _ = RefreshAsync();
        }
        else
        {
            base.SetShown(shown);
        }
    }

    protected override object Read(CancellationToken cancellationToken) => Context.Decoder.Decode(FilePath, cancellationToken);

    protected override void Show(object content)
    {
        var image = (DecodedImage)content;
        _released = false;
        Image = image;
        Picture = image.Picture;
        SuggestsExternalApp = false;
        Zoom.SetContent(image.Size);
        Summary = Format(Strings.ImageSummary, image.Size.Width, image.Size.Height, ImageFormats.Name(image.Format, FilePath), ByteSizes.Format(image.FileLength));
        Note = NoteFor(image);

        // The file changed while the tab was hidden: the info is updated, the picture waits until shown.
        if (_hidden)
        {
            ReleaseIfLarge();
        }
    }

    protected override string Describe(Exception exception) => exception switch
    {
        ImageDecodeException { Failure: ImageDecodeFailure.NoCodec, Format: ImageFormat.WebP } => Strings.WebPCodecMissing,
        ImageDecodeException { Failure: ImageDecodeFailure.NoCodec } noCodec => Format(Strings.ImageCodecMissing, ImageFormats.Name(noCodec.Format, FilePath)),
        ImageDecodeException damaged => Format(Strings.ImageDamaged, damaged.Message),
        _ => base.Describe(exception),
    };

    protected override void OnFailed(Exception exception) => SuggestsExternalApp = exception is ImageDecodeException;

    private void ReleaseIfLarge()
    {
        if (Image is { MemoryBytes: > KeepWhenHiddenBytes })
        {
            Picture = null;
            _released = true;
        }
    }

    private string? NoteFor(DecodedImage image)
    {
        var notes = new List<string>();
        if (image.IsReduced)
        {
            notes.Add(Format(Strings.ImageReduced, image.Decoded.Width, image.Decoded.Height));
        }

        if (image.FrameCount > 1 && ImageFormats.HasFrames(image.Format))
        {
            notes.Add(Format(image.Format == ImageFormat.Tiff ? Strings.ImageFirstPage : Strings.ImageFirstFrame, image.FrameCount));
        }

        return notes.Count == 0 ? null : string.Join(NoteSeparator, notes);
    }
}
