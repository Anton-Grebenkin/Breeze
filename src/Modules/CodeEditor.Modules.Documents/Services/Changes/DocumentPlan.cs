using CodeEditor.Modules.Agent.Contracts.Approvals;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// A ready document edit: the new file bytes, the card showing what changes, and the model's reply after writing. Built
/// entirely in memory, both for the card and for the write.
/// </summary>
public sealed record DocumentPlan(DocumentPath Target, byte[] Bytes, FileChangePreview Preview, string Report);
