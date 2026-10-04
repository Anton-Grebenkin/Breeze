using CodeEditor.Modules.Agent.Contracts;

namespace CodeEditor.Modules.Diagrams.Tests.Infrastructure;

/// <summary>Images for the model: records shown ones; the test sets whether the model sees images and the size limit.</summary>
internal sealed class FakeAgentImages : IAgentImages
{
    public bool CanShow { get; set; } = true;

    public long MaxBytes { get; set; } = long.MaxValue;

    public List<(string Name, byte[] Data, string MediaType)> Shown { get; } = [];

    public bool TryShow(string name, byte[] data, string mediaType)
    {
        if (data.Length > MaxBytes)
        {
            return false;
        }

        Shown.Add((name, data, mediaType));
        return true;
    }
}
