using CodeEditor.Modules.Docker.Services.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>An image: name:tag, size and age.</summary>
public sealed partial class ImageNodeViewModel : DockerNodeViewModel
{
    public ImageNodeViewModel(DockerImage image, DateTimeOffset now)
        : base(KeyOf(image))
    {
        Image = image;
        Show(image, now);
    }

    public override DockerNodeKind Kind => DockerNodeKind.Image;

    public override string AutomationId => "Docker.Image." + Image.Title;

    [ObservableProperty]
    public partial DockerImage Image { get; private set; }

    /// <summary>One image with several tags is several rows, so the key includes the name.</summary>
    public static string KeyOf(DockerImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return "image:" + image.Id + ":" + image.Title;
    }

    public void Update(DockerImage image, DateTimeOffset now)
    {
        Image = image;
        Show(image, now);
    }

    private void Show(DockerImage image, DateTimeOffset now)
    {
        Title = image.Title;
        Description = DockerNodeText.Image(image, now);
        ToolTip = DockerNodeText.ImageToolTip(image);
        Icon = DockerIcons.Image;
        Tone = image.IsDangling ? DockerTone.Muted : DockerTone.Neutral;
    }
}
