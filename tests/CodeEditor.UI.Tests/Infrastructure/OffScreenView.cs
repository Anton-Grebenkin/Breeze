using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodeEditor.UI.Themes;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// A view of the product in a window far off the screen, with the app's theme: its markup is loaded, laid out and
/// bound for real, and its controls are operated through their automation peers — no mouse or keyboard, so it works
/// while the user works or the screen is locked. Binding errors are collected. All such views share one UI thread
/// with one application object: a process can have only one, and markup looks its styles up in it.
/// </summary>
internal sealed class OffScreenView : IDisposable
{
    private const double OffScreen = -20_000;

    private static readonly string[] ThemeFiles = ["Typography", "Controls", "Inputs", "ScrollBars", "Menus", "Trees", "Brand"];

    private static readonly Lazy<Dispatcher> Ui = new(StartApplication);

    private readonly Dispatcher _dispatcher = Ui.Value;
    private readonly BindingErrors _errors = new();
    private Window? _window;

    public OffScreenView()
    {
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(_errors);
    }

    /// <summary>What the bindings of the shown view complained about.</summary>
    public IReadOnlyList<string> BindingFailures => [.. _errors.Messages];

    /// <summary>Creates the view on the UI thread and shows it; the theme is the app's dark one.</summary>
    public void Show(Func<FrameworkElement> create) => _dispatcher.Invoke(() =>
    {
        _window = new Window
        {
            Left = OffScreen, Top = OffScreen, Width = 480, Height = 900,
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Content = create(),
        };
        _window.Show();
    });

    /// <summary>Runs on the UI thread, after the layout and bindings caught up.</summary>
    public T Read<T>(Func<T> read)
    {
        Settle();
        return _dispatcher.Invoke(read);
    }

    public void Run(Action action)
    {
        Settle();
        _dispatcher.Invoke(action);
        Settle();
    }

    public bool IsShown(string automationId) => Read(() => Find(automationId) is { IsVisible: true });

    /// <summary>Presses a button the way a screen reader would.</summary>
    public void Press(string automationId) => Run(() =>
    {
        var button = Element<ButtonBase>(automationId);
        if (UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
        {
            toggle.Toggle();
        }
        else
        {
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();
        }
    });

    public void Type(string automationId, string text) => Run(() =>
    {
        switch (Element<Control>(automationId))
        {
            case TextBox box:
                box.Text = text;
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                break;
            case PasswordBox box:
                box.Password = text;
                break;
            default:
                throw new InvalidOperationException($"{automationId} is not a text field.");
        }
    });

    public string TextOf(string automationId) => Read(() => Element<FrameworkElement>(automationId) switch
    {
        TextBlock block => block.Text,
        TextBox box => box.Text,
        ContentControl { Content: string text } => text,
        var element => throw new InvalidOperationException($"{automationId} ({element.GetType().Name}) has no text."),
    });

    /// <summary>The headers of the menu a button opens under itself.</summary>
    public IReadOnlyList<string> MenuOf(string automationId) => Read(() =>
    {
        var menu = Element<FrameworkElement>(automationId).ContextMenu ?? throw new InvalidOperationException($"{automationId} has no menu.");
        return (IReadOnlyList<string>)[.. menu.Items.Cast<object>().Select(static item => item.ToString() ?? string.Empty)];
    });

    /// <summary>Renders the view to a PNG next to the other test screenshots and returns its path.</summary>
    public string SaveScreenshot(string name) => Read(() =>
    {
        var content = (FrameworkElement)_window!.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "screenshots")).FullName;
        var path = Path.Combine(folder, name + ".png");
        using var file = File.Create(path);
        encoder.Save(file);
        return path;
    });

    public void Dispose()
    {
        _dispatcher.Invoke(() => _window?.Close());
        PresentationTraceSources.DataBindingSource.Listeners.Remove(_errors);
    }

    // The application object with the theme the app merges in App.xaml; it lives as long as the test process.
    private static Dispatcher StartApplication()
    {
        var started = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = ThemeDictionaries.DarkColors });
                foreach (var file in ThemeFiles)
                {
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/CodeEditor.UI;component/Themes/{file}.xaml") });
                }

                started.SetResult(Dispatcher.CurrentDispatcher);
            }
            catch (Exception exception)
            {
                started.SetException(exception);
                return;
            }

            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return started.Task.GetAwaiter().GetResult();
    }

    // Bindings, layout and item containers are produced at lower priorities than a direct call.
    private void Settle() => _dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

    private T Element<T>(string automationId)
        where T : FrameworkElement =>
        Find(automationId) as T ?? throw new InvalidOperationException($"Элемент {automationId} не найден в представлении.");

    private FrameworkElement? Find(string automationId) => Descendants(_window!).FirstOrDefault(element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == automationId);

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element)
            {
                yield return element;
            }

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class BindingErrors : TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                lock (Messages)
                {
                    Messages.Add(message);
                }
            }
        }
    }
}
