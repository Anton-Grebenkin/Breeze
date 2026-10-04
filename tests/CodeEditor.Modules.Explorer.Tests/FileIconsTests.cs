using CodeEditor.Modules.Explorer.Services;
using CodeEditor.Shell;

namespace CodeEditor.Modules.Explorer.Tests;

/// <summary>File icons by extension; unknown or missing extensions get the generic file icon.</summary>
public sealed class FileIconsTests
{
    [Theory]
    [InlineData("Program.cs", IconNames.FileCode)]
    [InlineData(".gitignore", IconNames.FileText)]
    [InlineData("Makefile", IconNames.File)]
    [InlineData("report.PDF", IconNames.FilePdf)]
    [InlineData("letter.docx", IconNames.FileText)]
    [InlineData("sales.xlsx", IconNames.Table)]
    [InlineData("data.csv", IconNames.Table)]
    [InlineData("slides.pptx", IconNames.Presentation)]
    [InlineData("arch.mmd", IconNames.Diagram)]
    [InlineData("scan.TIFF", IconNames.FileMedia)]
    [InlineData("song.flac", IconNames.Audio)]
    [InlineData("clip.webm", IconNames.Video)]
    [InlineData("archive.bin", IconNames.File)]
    public void Icon_ComesFromTheExtension(string name, string icon) => Assert.Equal(icon, FileIcons.ForFile(name));
}
