using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// The Windows clipboard for paste scenarios. The clipboard needs an STA thread, and test threads are MTA, so each call
/// runs on its own STA thread; the data is copied to the clipboard and stays after the thread ends.
/// </summary>
public static class TestClipboard
{
    public static void SetFiles(params string[] paths)
    {
        var list = new StringCollection();
        list.AddRange(paths);
        OnSta(() => Clipboard.SetFileDropList(list));
    }

    /// <summary>A plain gray picture, as a screenshot tool puts it.</summary>
    public static void SetImage(int width, int height)
    {
        const int bytesPerPixel = 4;
        const byte gray = 0x80;
        var pixels = Enumerable.Repeat(gray, width * height * bytesPerPixel).ToArray();
        OnSta(() => Clipboard.SetImage(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * bytesPerPixel)));
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("Не удалось записать в буфер обмена.", failure);
        }
    }
}
