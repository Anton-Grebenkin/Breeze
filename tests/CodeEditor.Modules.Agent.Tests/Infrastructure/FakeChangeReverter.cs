using System.Text;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>Editor-less revert: writes the original text to the file, or recycles a file the agent created.</summary>
internal sealed class FakeChangeReverter(FakeFileSystem fileSystem) : IAgentChangeReverter
{
    public List<string> Reverted { get; } = [];

    public Task RevertAsync(string path, string? originalText)
    {
        Reverted.Add(path);
        if (originalText is null)
        {
            fileSystem.DeleteToRecycleBin(path);
        }
        else
        {
            fileSystem.WriteAllBytesAtomic(path, Encoding.UTF8.GetBytes(originalText));
        }

        return Task.CompletedTask;
    }
}
