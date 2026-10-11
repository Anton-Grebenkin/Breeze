using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using CodeEditor.Shell.Wpf.Input;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>
/// What a drop or the clipboard can attach to a message: files (Windows, the explorer, an editor tab) and pictures
/// (a screenshot, an image copied in a browser). PNG goes first: it keeps transparency, while the bitmap format of a
/// browser copy loses it.
/// </summary>
internal static class AttachmentData
{
    private const string PngFormat = "PNG";

    public static bool HasAttachable(IDataObject data) => FileDragData.HasPaths(data) || HasImage(data);

    public static bool HasImage(IDataObject data) =>
        data.GetDataPresent(PngFormat) || data.GetDataPresent(DataFormats.Bitmap);

    /// <summary>Existing files of the data; folders are left out.</summary>
    public static IReadOnlyList<string> Files(IDataObject data) =>
        FileDragData.GetPaths(data) is { } paths ? [.. paths.Where(File.Exists)] : [];

    /// <summary>The picture as PNG bytes; <c>null</c> if there is none or it cannot be read.</summary>
    public static byte[]? Png(IDataObject data)
    {
        try
        {
            if (data.GetData(PngFormat) is MemoryStream stream)
            {
                return stream.ToArray();
            }

            return data.GetData(DataFormats.Bitmap) is BitmapSource bitmap ? Encode(bitmap) : null;
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Plain text of the data; <c>null</c> without text.</summary>
    public static string? Text(IDataObject data)
    {
        try
        {
            return data.GetDataPresent(DataFormats.UnicodeText) ? data.GetData(DataFormats.UnicodeText) as string : null;
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The clipboard, or <c>null</c> while another program holds it.</summary>
    public static IDataObject? Clipboard()
    {
        try
        {
            return System.Windows.Clipboard.GetDataObject();
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static byte[] Encode(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
