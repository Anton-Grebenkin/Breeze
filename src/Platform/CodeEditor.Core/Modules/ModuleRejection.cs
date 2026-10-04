namespace CodeEditor.Core.Modules;

/// <summary>
/// A module the catalog refused to load.
/// </summary>
/// <param name="RelatedModuleId">The dependency that caused the rejection, if any.</param>
public sealed record ModuleRejection(string ModuleId, ModuleRejectionReason Reason, string? RelatedModuleId = null);
