namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>What an Excel number format shows.</summary>
internal enum NumberKind
{
    /// <summary>A plain number.</summary>
    Number,

    /// <summary>A date, possibly with a time.</summary>
    Date,

    /// <summary>Only a time of day: "h:mm:ss", "mm:ss".</summary>
    Time,

    /// <summary>A duration: "[h]:mm" shows 36 hours, not a day and a half.</summary>
    Duration,
}
