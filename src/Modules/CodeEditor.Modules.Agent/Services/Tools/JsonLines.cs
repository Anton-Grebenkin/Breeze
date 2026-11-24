using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// Rewrites JSON that came as one long line (an API answer, <c>kubectl -o json</c>) with indentation: the saved output
/// is then read by lines, and its head shows the structure. Other lines stay as they are. O(n) in text length.
/// </summary>
internal static class JsonLines
{
    /// <summary>Shorter lines read well as they are.</summary>
    public const int MinLineLength = 2_000;

    private static readonly JsonWriterOptions Indented = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions Lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static string Unfold(string text)
    {
        var lines = text.Split('\n');
        if (!lines.Any(IsCandidate))
        {
            return text;
        }

        return string.Join('\n', lines.Select(line => IsCandidate(line) ? Indent(line) : line));
    }

    private static bool IsCandidate(string line) => line.Length >= MinLineLength && line.AsSpan().TrimStart() is ['{' or '[', ..];

    private static string Indent(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line, Lenient);
            using var stream = new MemoryStream(line.Length * 2);
            using (var writer = new Utf8JsonWriter(stream, Indented))
            {
                document.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }
        catch (JsonException)
        {
            return line;
        }
    }
}
