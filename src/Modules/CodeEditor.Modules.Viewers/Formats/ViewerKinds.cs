using System.Collections.Frozen;

namespace CodeEditor.Modules.Viewers.Formats;

/// <summary>
/// Viewer kind by extension. Only extensions that are never text count as binary: <c>.obj</c> (Wavefront OBJ) and
/// <c>.dat</c> are often text, so such a file is shown as bytes only if it fails to open as text.
/// </summary>
public static class ViewerKinds
{
    private static readonly FrozenDictionary<string, ViewerKind> ByExtension = Build();

    public static ViewerKind Of(string path) =>
        ByExtension.TryGetValue(Path.GetExtension(path), out var kind) ? kind : ViewerKind.None;

    private static FrozenDictionary<string, ViewerKind> Build()
    {
        string[] images = [".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".ico", ".tif", ".tiff", ".webp"];
        string[] audio = [".mp3", ".wav", ".ogg", ".oga", ".flac", ".m4a", ".aac", ".opus"];
        string[] video = [".mp4", ".webm", ".ogv", ".mov", ".m4v"];
        string[] binary = [".bin", ".exe", ".dll", ".pdb", ".so", ".dylib", ".wasm", ".class", ".o", ".lib", ".a", ".db", ".sqlite"];

        return images.Select(static extension => (Extension: extension, Kind: ViewerKind.Image))
            .Concat(audio.Select(static extension => (Extension: extension, Kind: ViewerKind.Audio)))
            .Concat(video.Select(static extension => (Extension: extension, Kind: ViewerKind.Video)))
            .Concat(binary.Select(static extension => (Extension: extension, Kind: ViewerKind.Binary)))
            .Append((Extension: ".svg", Kind: ViewerKind.Svg))
            .ToFrozenDictionary(static pair => pair.Extension, static pair => pair.Kind, StringComparer.OrdinalIgnoreCase);
    }
}
