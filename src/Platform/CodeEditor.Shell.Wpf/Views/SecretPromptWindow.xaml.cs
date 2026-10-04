using System.Windows;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>Masked key input window: <c>Enter</c> saves, <c>Esc</c> cancels; an optional link says where to get the key.</summary>
public sealed partial class SecretPromptWindow
{
    private readonly Uri? _link;
    private readonly Action<Uri> _open;

    public SecretPromptWindow(string title, string message, Uri? link, Action<Uri> open)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        _link = link;
        _open = open;
        if (link is not null)
        {
            LinkRun.Text = link.Host;
            LinkLine.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => SecretBox.Focus();
    }

    /// <summary>The secret entered before Save; empty input counts as cancel.</summary>
    public string? Secret { get; private set; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Secret = string.IsNullOrWhiteSpace(SecretBox.Password) ? null : SecretBox.Password.Trim();
        DialogResult = Secret is not null;
    }

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (_link is not null)
        {
            _open(_link);
        }
    }
}
