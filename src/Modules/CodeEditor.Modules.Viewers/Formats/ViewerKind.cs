namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>How a file is shown: image, SVG drawing, audio, video or bytes.</summary>
public enum ViewerKind
{
    /// <summary>Not for this module: the text editor or another viewer opens the file.</summary>
    None,

    /// <summary>Raster image, decoded by Windows (WIC).</summary>
    Image,

    /// <summary>Vector drawing, shown in a WebView2 page.</summary>
    Svg,

    Audio,

    Video,

    /// <summary>Binary file, shown in the hex view.</summary>
    Binary,
}
