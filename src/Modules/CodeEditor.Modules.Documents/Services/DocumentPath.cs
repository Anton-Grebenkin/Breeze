using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>A workspace document: full path, root-relative path with "/" and format by extension.</summary>
public readonly record struct DocumentPath(string Full, string Relative, DocumentKind Kind)
{
    public string Name => Path.GetFileName(Full);
}
