using CodeEditor.Shell.Palette;

namespace CodeEditor.Shell.Tests.Palette;

public sealed class RecentCommandsTests
{
    [Fact]
    public void Add_MovesCommandToTopWithoutDuplicates()
    {
        var recent = new RecentCommands();

        recent.Add("a");
        recent.Add("b");
        recent.Add("a");

        Assert.Equal(["a", "b"], recent.Items);
    }

    [Fact]
    public void Add_DropsOldestBeyondCapacity()
    {
        var recent = new RecentCommands();

        for (var i = 0; i <= RecentCommands.Capacity; i++)
        {
            recent.Add($"command.{i}");
        }

        Assert.Equal(RecentCommands.Capacity, recent.Items.Count);
        Assert.DoesNotContain("command.0", recent.Items);
        Assert.Equal($"command.{RecentCommands.Capacity}", recent.Items[0]);
    }
}
