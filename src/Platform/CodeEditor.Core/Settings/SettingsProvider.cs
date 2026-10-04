using Microsoft.Extensions.Configuration;

namespace CodeEditor.Core.Settings;

/// <summary>A settings layer (user or folder) whose data <see cref="SettingsService"/> supplies.</summary>
internal sealed class SettingsProvider : ConfigurationProvider
{
    public void Replace(Dictionary<string, string?> data)
    {
        Data = data;
        OnReload();
    }
}
