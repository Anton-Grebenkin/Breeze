using System.Globalization;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>A build error or warning: full file path (if any), line, column, code and message.</summary>
public sealed record BuildDiagnostic(string? File, int Line, int Column, bool IsError, string Code, string Message)
{
    /// <summary>Line for the model and the output channel: <c>src/A.cs(12,5): error CS1002: ; expected</c>.</summary>
    public string Format(Func<string, string> relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        var severity = IsError ? "error" : "warning";
        var location = File is null
            ? string.Empty
            : Line > 0 ? string.Create(CultureInfo.InvariantCulture, $"{relative(File)}({Line},{Column}): ") : relative(File) + ": ";
        return $"{location}{severity} {Code}: {Message}";
    }
}
