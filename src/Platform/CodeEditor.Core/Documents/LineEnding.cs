namespace CodeEditor.Core.Documents;

public enum LineEnding
{
    /// <summary><c>\r\n</c>: Windows.</summary>
    CrLf,

    /// <summary><c>\n</c>: Linux, macOS, most repositories.</summary>
    Lf,
}
