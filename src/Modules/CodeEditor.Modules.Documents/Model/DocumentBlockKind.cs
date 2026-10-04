namespace CodeEditor.Modules.Documents.Model;

/// <summary>Document block kind: both the model's Markdown and the user's view are built from blocks.</summary>
public enum DocumentBlockKind
{
    Paragraph,
    Heading,
    ListItem,
    Table,
    Code,
    Quote,

    /// <summary>Horizontal rule.</summary>
    Rule,

    /// <summary>Start of a presentation slide: number and title.</summary>
    Slide,

    /// <summary>Speaker notes for a slide.</summary>
    Notes,
}
