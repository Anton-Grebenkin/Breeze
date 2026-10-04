namespace CodeEditor.Modules.Documents.Model;

/// <summary>Cell value type.</summary>
public enum CellKind
{
    Text,
    Number,
    Boolean,

    /// <summary>Date or time: an Excel number with a date format.</summary>
    Date,

    /// <summary>Formula error: <c>#DIV/0!</c>, <c>#REF!</c>.</summary>
    Error,
}
