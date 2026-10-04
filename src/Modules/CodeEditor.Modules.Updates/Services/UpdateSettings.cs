namespace CodeEditor.Modules.Updates.Services;

/// <summary>Update settings: the <c>update</c> section of settings.json, e.g. <c>"update.mode": "manual"</c>.</summary>
public sealed class UpdateSettings
{
    public const string Section = "update";

    public UpdateMode Mode { get; set; } = UpdateMode.Default;
}
