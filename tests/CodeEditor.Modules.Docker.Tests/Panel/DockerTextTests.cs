using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;
using CodeEditor.Modules.Docker.ViewModels.Tree;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// docker's English texts in the UI language: durations with docker's rounding steps, container status with exit code
/// and health, image size. A stop by signal (docker stop) is not a failure.
/// </summary>
public sealed class DockerTextTests
{
    [Theory]
    [InlineData("45 hours", 45 * 60)]
    [InlineData("About an hour", 60)]
    [InlineData("About a minute", 1)]
    [InlineData("2 weeks", 14 * 24 * 60)]
    [InlineData("Less than a second", 0)]
    public void Durations_AreParsed(string text, int minutes) =>
        Assert.Equal(minutes == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(minutes), DockerTime.ParseDuration(text));

    [Fact]
    public void UnknownDuration_IsNull() => Assert.Null(DockerTime.ParseDuration("a while"));

    [Theory]
    [InlineData(30, "30 с")]
    [InlineData(45 * 3600, "45 ч")]
    [InlineData(13 * 86400, "13 дн.")]
    [InlineData(14 * 86400, "2 нед.")]
    [InlineData(60 * 86400, "2 мес.")]
    [InlineData(730 * 86400, "2 г.")]
    public void Durations_AreShownLikeDocker(int seconds, string expected) =>
        Assert.Equal(expected, DockerTime.Format(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData("Up 45 hours (healthy)", "running", "работает 45 ч")]
    [InlineData("Up 3 minutes (unhealthy)", "running", "работает 3 мин · проверка не пройдена")]
    [InlineData("Up 2 days (Paused)", "paused", "приостановлен")]
    [InlineData("Exited (143) 2 months ago", "exited", "остановлен 2 мес. назад")]
    [InlineData("Exited (1) 5 seconds ago", "exited", "завершился с кодом 1 5 с назад")]
    [InlineData("Created", "created", "создан, не запускался")]
    public void ContainerState_IsShownInInterfaceLanguage(string status, string state, string expected)
    {
        var container = new DockerContainer("id", "api", "app", Enum.Parse<ContainerState>(state, ignoreCase: true), status);

        Assert.Equal(expected + " · app", DockerNodeText.Container(container));
    }

    [Fact]
    public void StopBySignal_IsNotAFailure()
    {
        Assert.False(ContainerStatus.Parse("Exited (137) 1 hour ago").IsFailure);
        Assert.True(ContainerStatus.Parse("Exited (2) 1 hour ago").IsFailure);
        Assert.Equal(default, ContainerStatus.Parse("Removal In Progress"));
    }

    [Theory]
    [InlineData("113MB", "113 МБ")]
    [InlineData("9.73MB", "9,73 МБ")]
    [InlineData("1.32GB", "1,32 ГБ")]
    [InlineData("0B", "0 Б")]
    [InlineData("13.9kB", "13,9 КБ")]
    [InlineData("big", "big")]
    public void ImageSize_IsLocalized(string size, string expected) => Assert.Equal(expected, DockerNodeText.Size(size));

    [Theory]
    [InlineData(1, 4, "1 из 4 работает")]
    [InlineData(3, 4, "3 из 4 работают")]
    [InlineData(0, 2, "0 из 2 работают")]
    public void RunningSummary_AgreesWithTheNumber(int running, int total, string expected) =>
        Assert.Equal(expected, DockerNodeText.Running(running, total));
}
