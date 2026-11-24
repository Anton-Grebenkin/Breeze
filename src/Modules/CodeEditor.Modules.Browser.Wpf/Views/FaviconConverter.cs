using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeEditor.Modules.Browser.Wpf.Views;

/// <summary>
/// A site icon (PNG bytes from the page) as an image for the tab strip. The decoded image is kept per icon, so a tab
/// that only changes its title does not decode it again.
/// </summary>
internal sealed class FaviconConverter : IValueConverter
{
    private const int IconSize = 16;

    public static FaviconConverter Instance { get; } = new();

    private readonly ConditionalWeakTable<byte[], ImageSource> _decoded = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ReadOnlyMemory<byte> { IsEmpty: false } icon || !MemoryMarshal.TryGetArray(icon, out var bytes) || bytes.Array is not { } array)
        {
            return null;
        }

        if (!_decoded.TryGetValue(array, out var image) && Decode(array) is { } decoded)
        {
            _decoded.AddOrUpdate(array, image = decoded);
        }

        return image;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;

    // A page may hand over anything as its icon: what does not decode is simply not shown.
    private static BitmapImage? Decode(byte[] bytes)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = IconSize * 2;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
