using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodeEditor.Modules.Viewers.Formats;

namespace CodeEditor.Modules.Viewers.Wpf.Services;

/// <summary>
/// A display-ready picture from a decoder frame: scaled while decoding (WIC Fant, so the full-size frame never reaches
/// memory), pixels loaded at once (<see cref="CachedBitmap"/>) so the UI thread only draws. The EXIF rotation runs on
/// the in-memory pixels: a 90° turn reads columns, which for the JPEG decoder means decoding again.
/// </summary>
internal static class WicPictures
{
    public static BitmapSource Prepare(BitmapSource frame, PixelSize target, ImageOrientation orientation)
    {
        BitmapSource source = frame;
        if (target.Width != frame.PixelWidth || target.Height != frame.PixelHeight)
        {
            source = new TransformedBitmap(source, new ScaleTransform((double)target.Width / frame.PixelWidth, (double)target.Height / frame.PixelHeight));
        }

        source = Load(source);
        if (!orientation.IsUpright)
        {
            source = Load(new TransformedBitmap(source, Turn(orientation)));
        }

        source.Freeze();
        return source;
    }

    private static CachedBitmap Load(BitmapSource source) => new(source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

    // Flip first, then rotate clockwise, as in ImageOrientation.
    private static TransformGroup Turn(ImageOrientation orientation)
    {
        var turn = new TransformGroup();
        if (orientation.FlipHorizontal)
        {
            turn.Children.Add(new ScaleTransform(-1, 1));
        }

        if (orientation.Rotation != 0)
        {
            turn.Children.Add(new RotateTransform(orientation.Rotation));
        }

        return turn;
    }
}
