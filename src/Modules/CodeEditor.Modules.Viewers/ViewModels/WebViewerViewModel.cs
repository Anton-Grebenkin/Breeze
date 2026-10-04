using System.Globalization;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// A viewer in a WebView2 page: SVG drawing, audio, video. The view model only checks the file and gives the page its
/// address (<see cref="ViewerAddresses"/>): the host is the file's folder and the version is the modification time plus
/// the load number, so a changed file is shown anew rather than from the browser cache. Page replies go to
/// <see cref="Receive"/>.
/// </summary>
public abstract partial class WebViewerViewModel(string filePath, ViewerKind kind, ViewerContext context)
    : ViewerViewModel(filePath, kind, context)
{
    /// <summary>The file address for the page; <c>null</c> until the file is read.</summary>
    [ObservableProperty]
    public partial Uri? Source { get; private set; }

    /// <summary>Load number: grows with every file read so the page shows the file anew.</summary>
    [ObservableProperty]
    public partial int Revision { get; private set; }

    /// <summary>The file's folder, which the virtual host exposes to the page.</summary>
    public string Folder => Path.GetDirectoryName(Path.GetFullPath(FilePath)) ?? FilePath;

    public string Host => ViewerAddresses.HostFor(Folder);

    protected long FileLength { get; private set; }

    /// <summary>A page message: drawing size, zoom, recording metadata, error.</summary>
    public abstract void Receive(ViewerPageMessage message);

    protected override object Read(CancellationToken cancellationToken)
    {
        var files = Context.FileSystem;
        if (!files.FileExists(FilePath))
        {
            throw new FileNotFoundException(Strings.FileMissing, FilePath);
        }

        return new FileStamp(files.GetFileLength(FilePath), files.GetLastWriteTimeUtc(FilePath));
    }

    protected override void Show(object content)
    {
        var stamp = (FileStamp)content;
        FileLength = stamp.Length;
        Source = ViewerAddresses.FileAddress(FilePath, string.Create(CultureInfo.InvariantCulture, $"{stamp.Written.Ticks}-{Revision + 1}"));
        Revision++;
        UpdateSummary();
    }

    /// <summary>Updates the tab info from what is known so far.</summary>
    protected abstract void UpdateSummary();

    private sealed record FileStamp(long Length, DateTime Written);
}
